using Microsoft.Maui.Storage;

namespace Mozdulj.Maui.Services;

/// <summary>Completely offline audio. The original background pad ships with the APK.</summary>
public sealed class WorkoutAudio : IDisposable
{
#if ANDROID
    private global::Android.Media.MediaPlayer? _music;
    private global::Android.Media.ToneGenerator? _beeper;
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
    public async Task StartRelaxationAsync(string? personalAudioFileName=null)
    {
#if ANDROID
        if(_music is not null) { if(!_music.IsPlaying) _music.Start(); return; }
        try
        {
            var safeName=Path.GetFileName(personalAudioFileName ?? "");
            var custom=!string.IsNullOrWhiteSpace(safeName) && safeName.StartsWith("relax-user-",StringComparison.Ordinal) &&
                safeName==personalAudioFileName ? Path.Combine(FileSystem.Current.AppDataDirectory,safeName) : "";
            var file=!string.IsNullOrWhiteSpace(custom) && File.Exists(custom)
                ? custom : Path.Combine(FileSystem.Current.CacheDirectory,"relax-ambient-v254.wav");
            if(!File.Exists(file))
            {
                await using var source=await FileSystem.Current.OpenAppPackageFileAsync("audio/relax-ambient.wav");
                await using var dest=File.Create(file);
                await source.CopyToAsync(dest);
            }
            _music=new global::Android.Media.MediaPlayer();
            _music.SetDataSource(file);
            _music.Looping=true;
            // Gentle but clearly audible on phone speakers; the replacement WAV has
            // safe peaks and no sub-bass that could overload tiny speakers.
            _music.SetVolume(file==custom ? .72f : .65f, file==custom ? .72f : .65f);
            _music.Prepare();
            _music.Start();
        }
        catch { Stop(); /* No background audio is safer than playback failure. */ }
#else
        await Task.CompletedTask;
#endif
    }
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
        try { _music?.Stop(); } catch { }
        _music?.Release(); _music?.Dispose(); _music=null;
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
