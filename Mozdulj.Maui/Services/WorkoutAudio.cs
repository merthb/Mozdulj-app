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
#endif
    }
    /// <summary>Play the selected on-device files in order, repeating as needed
    /// until the relaxation timer finishes. Never copies or streams YouTube music.</summary>
    public async Task StartRelaxationAsync(
        IEnumerable<RelaxationTrack>? tracks = null, string? legacyFileName = null)
    {
#if ANDROID
        Stop();
        try
        {
            _musicFiles.Clear();
            foreach (var track in tracks ?? [])
            {
                var fullPath = ResolveCustomAudio(track.FileName);
                if (fullPath is not null) _musicFiles.Add(fullPath);
            }

            // Existing installs retain their former one-file selection.
            if (_musicFiles.Count == 0)
            {
                var oldFile = ResolveCustomAudio(legacyFileName);
                if (oldFile is not null) _musicFiles.Add(oldFile);
            }
            if (_musicFiles.Count == 0)
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

            _musicRunning = true;
            _musicIndex = 0;
            PlayNextTrack();
        }
        catch { Stop(); /* Silent fallback: the timer must remain independent. */ }
#else
        await Task.CompletedTask;
#endif
    }

#if ANDROID
    private static string? ResolveCustomAudio(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            Path.GetFileName(fileName) != fileName ||
            !fileName.StartsWith("relax-user-", StringComparison.Ordinal))
            return null;
        var path = Path.Combine(FileSystem.Current.AppDataDirectory, fileName);
        return File.Exists(path) ? path : null;
    }

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
                try
                {
                    var player = new global::Android.Media.MediaPlayer();
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
                    _music?.Release();
                    _music?.Dispose();
                    _music = null;
                }
            }
        }
        catch { /* A silent track is better than a crashed workout. */ }
        Stop();
    }
#endif
    public void SetPaused(bool paused)
    {
#if ANDROID
        try {if(_music is not null){if(paused && _music.IsPlaying)_music.Pause();
            else if(!paused && !_music.IsPlaying)_music.Start();}}
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
#endif
    }
    public void Dispose()
    {
        Stop();
#if ANDROID
        _beeper?.Release(); _beeper?.Dispose(); _beeper=null;
#endif
    }
}
