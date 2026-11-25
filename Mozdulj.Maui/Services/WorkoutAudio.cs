using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel;
using Mozdulj.Core;

namespace Mozdulj.Maui.Services;

/// <summary>Completely offline audio. The original background pad ships with the APK.</summary>
public sealed class WorkoutAudio : IDisposable
{
#if ANDROID
    private global::Android.Media.MediaPlayer? _music;
    private global::Android.Media.ToneGenerator? _beeper;
    private readonly List<string> _musicFiles = [];
    private int _musicIndex;
    private bool _musicRunning;
#elif IOS
    private AVFoundation.AVAudioPlayer? _music;
    private AVFoundation.AVAudioPlayer? _beeper;
    private readonly List<string> _musicFiles = [];
    private int _musicIndex;
    private bool _musicRunning;
#endif
    public void Countdown(int secondsRemaining)
    {
        if(secondsRemaining is < 0 or > 3) return;
#if ANDROID
        try
        {
            _beeper ??= new global::Android.Media.ToneGenerator(
                global::Android.Media.Stream.Music, 95);
            _beeper.StopTone();
            // 3, 2, 1: three short beeps. 0: a distinct, longer closing tone.
            _beeper.StartTone(secondsRemaining==0 ? global::Android.Media.Tone.PropAck
                : global::Android.Media.Tone.PropBeep, secondsRemaining==0 ? 230 : 130);
        }
        catch { /* Audio unavailable: never interrupt the exercise. */ }
#elif IOS
        try
        {
            _beeper?.Stop();
            _beeper?.Dispose();
            var frequency = secondsRemaining == 0 ? 960 : 680;
            var file = Path.Combine(FileSystem.Current.CacheDirectory, $"countdown-{frequency}.wav");
            if (!File.Exists(file)) File.WriteAllBytes(file, CreateToneWav(frequency));
            _beeper = AVFoundation.AVAudioPlayer.FromUrl(Foundation.NSUrl.FromFilename(file));
            _beeper?.PrepareToPlay();
            _beeper?.Play();
        }
        catch { /* Timers must continue without audio. */ }
#endif
    }
    /// <summary>Play the selected on-device files in order, repeating as needed
    /// until the relaxation timer finishes. Never copies or streams YouTube music.</summary>
    public async Task StartRelaxationAsync(
        IEnumerable<RelaxationTrack>? tracks = null, string? legacyFileName = null, bool useBuiltIn = false)
    {
#if ANDROID
        Stop();
        try
        {
            _musicFiles.Clear();
            if (!useBuiltIn)
            {
                foreach (var track in tracks ?? [])
                {
                    var fullPath = ResolveCustomAudio(track.FileName);
                    if (fullPath is not null) _musicFiles.Add(fullPath);
                }
            }

            // Existing installs retain their former one-file selection.
            if (!useBuiltIn && _musicFiles.Count == 0)
            {
                var oldFile = ResolveCustomAudio(legacyFileName);
                if (oldFile is not null) _musicFiles.Add(oldFile);
            }
            // Never silently replace a missing custom playlist with bundled audio.
            if (useBuiltIn)
            {
                var file = Path.Combine(FileSystem.Current.CacheDirectory, "relax-ambient-v254.wav");
                if (!File.Exists(file))
                {
                    await using var source = await FileSystem.Current.OpenAppPackageFileAsync("audio/relax-ambient.wav");
                    await using var dest = File.Create(file);
                    await source.CopyToAsync(dest);
                }
                _musicFiles.Add(file);
            }

            if (_musicFiles.Count == 0) return;
            _musicRunning = true;
            _musicIndex = 0;
            PlayNextTrack();
        }
        catch { Stop(); /* Silent fallback: the timer must remain independent. */ }
#elif IOS
        Stop();
        try
        {
            _musicFiles.Clear();
            if (!useBuiltIn)
            {
                foreach (var track in tracks ?? [])
                {
                    var path = ResolveCustomAudio(track.FileName);
                    if (path != null) _musicFiles.Add(path);
                }
                if (_musicFiles.Count == 0)
                {
                    var oldPath = ResolveCustomAudio(legacyFileName);
                    if (oldPath != null) _musicFiles.Add(oldPath);
                }
            }
            else
            {
                var path = Path.Combine(FileSystem.Current.CacheDirectory, "relax-ambient-v254.wav");
                if (!File.Exists(path))
                {
                    await using var source = await FileSystem.Current.OpenAppPackageFileAsync("audio/relax-ambient.wav");
                    await using var dest = File.Create(path);
                    await source.CopyToAsync(dest);
                }
                _musicFiles.Add(path);
            }
            if (_musicFiles.Count == 0) return;
            _musicRunning = true;
            _musicIndex = 0;
            PlayNextTrackIos();
        }
        catch { Stop(); }
#else
        await Task.CompletedTask;
#endif
    }

#if ANDROID || IOS
    private static string? ResolveCustomAudio(string? fileName)
    {
        return RelaxationAudioReferences.IsAvailable(FileSystem.Current.AppDataDirectory, fileName)
            ? RelaxationAudioReferences.ResolvePath(FileSystem.Current.AppDataDirectory, fileName)
            : null;
    }

#endif
#if ANDROID
    private void PlayNextTrack()
    {
        if (!_musicRunning || _musicFiles.Count == 0) return;
        try
        {
            _music?.Release();
            _music?.Dispose();
            _music = null;
            // Each file plays to completion, then the next track starts.
            // At the end of the list, playback cycles until the ten-minute timer stops.
            for (var attempt = 0; attempt < _musicFiles.Count; attempt++)
            {
                var file = _musicFiles[_musicIndex % _musicFiles.Count];
                _musicIndex++;
                if (!File.Exists(file)) continue;
                global::Android.Media.MediaPlayer? player = null;
                try
                {
                    player = new global::Android.Media.MediaPlayer();
                    player.SetDataSource(file);
                    player.Looping = false;
                    player.SetVolume(.68f, .68f);
                    player.Completion += (_, _) => MainThread.BeginInvokeOnMainThread(PlayNextTrack);
                    player.Prepare();
                    _music = player;
                    player.Start();
                    return;
                }
                catch
                {
                    player?.Release();
                    player?.Dispose();
                    _music = null;
                }
            }
        }
        catch { /* A silent track is better than a crashed workout. */ }
        Stop();
    }
#endif

#if IOS
    private void PlayNextTrackIos()
    {
        if (!_musicRunning || _musicFiles.Count == 0) return;
        _music?.Stop();
        _music?.Dispose();
        _music = null;
        for (int attempt = 0; attempt < _musicFiles.Count; attempt++)
        {
            var path = _musicFiles[_musicIndex++ % _musicFiles.Count];
            if (!File.Exists(path)) continue;
            try
            {
                var player = AVFoundation.AVAudioPlayer.FromUrl(Foundation.NSUrl.FromFilename(path));
                if (player is null) continue;
                player.FinishedPlaying += (_, _) => MainThread.BeginInvokeOnMainThread(PlayNextTrackIos);
                player.Volume = .68f;
                if (!player.PrepareToPlay() || !player.Play())
                {
                    player.Dispose();
                    continue;
                }
                _music = player;
                return;
            }
            catch { /* Skip unreadable tracks. */ }
        }
        Stop();
    }

    // Offline PCM tones for the final 3/2/1/0 seconds; no streaming or platform asset.
    private static byte[] CreateToneWav(int hz)
    {
        const int sampleRate = 22050;
        const int sampleCount = 3308;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + sampleCount * 2);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16); writer.Write((short)1); writer.Write((short)1);
        writer.Write(sampleRate); writer.Write(sampleRate * 2);
        writer.Write((short)2); writer.Write((short)16);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(sampleCount * 2);
        for (var i = 0; i < sampleCount; i++)
        {
            var envelope = Math.Min(1d, Math.Min(i / 180d, (sampleCount - 1 - i) / 180d));
            writer.Write((short)(Math.Sin(i * Math.PI * 2 * hz / sampleRate) * 12000 * envelope));
        }
        return stream.ToArray();
    }
#endif

    public void SetPaused(bool paused)
    {
#if ANDROID
        try {if(_music is not null){if(paused && _music.IsPlaying)_music.Pause();
            else if(!paused && !_music.IsPlaying)_music.Start();}}
        catch { Stop(); }
#elif IOS
        try
        {
            if (paused) _music?.Pause();
            else _music?.Play();
        }
        catch { Stop(); }
#endif
    }
    public void Stop()
    {
#if ANDROID
        _musicRunning = false;
        try { _music?.Stop(); } catch { }
        _music?.Release(); _music?.Dispose(); _music=null;
        _musicFiles.Clear();
        _musicIndex = 0;
#elif IOS
        _musicRunning = false;
        _music?.Stop();
        _music?.Dispose();
        _music = null;
        _musicFiles.Clear();
        _musicIndex = 0;
#endif
    }
    public void Dispose()
    {
        Stop();
#if ANDROID
        _beeper?.Release(); _beeper?.Dispose(); _beeper=null;
#elif IOS
        _beeper?.Stop(); _beeper?.Dispose(); _beeper = null;
#endif
    }
}
