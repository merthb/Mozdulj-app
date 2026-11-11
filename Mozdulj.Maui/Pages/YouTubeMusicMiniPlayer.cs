using Microsoft.Maui.Dispatching;
using Mozdulj.Maui.Platforms.Android;

namespace Mozdulj.Maui.Pages;

/// <summary>
/// A compact Android MediaSession controller for the *existing* YouTube Music
/// session. This is not a YouTube player and does not access music streams.
/// </summary>
public sealed class YouTubeMusicMiniPlayer : ContentView
{
    private readonly IDispatcherTimer _timer;
    private readonly Label _track = UI.Label("", 15, true);
    private readonly Label _artist = UI.Label("", 12, color: UI.Muted);
    private readonly Label _status = UI.Label("", 12, color: UI.Muted);
    private readonly Button _access = UI.Button("Értesítés-hozzáférés beállítása", false);
    private readonly Button _previous = UI.Button("⏮", false);
    private readonly Button _playPause = UI.Button("▶", false);
    private readonly Button _next = UI.Button("⏭", false);
    private readonly HorizontalStackLayout _controls = new() { Spacing = 8 };

    public YouTubeMusicMiniPlayer(IDispatcher dispatcher)
    {
        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => Refresh();

        _previous.Clicked += (_, _) => Send(YouTubeMusicMediaBridge.MusicCommand.Previous);
        _playPause.Clicked += (_, _) => Send(YouTubeMusicMediaBridge.MusicCommand.PlayPause);
        _next.Clicked += (_, _) => Send(YouTubeMusicMediaBridge.MusicCommand.Next);
        _access.Clicked += (_, _) => YouTubeMusicMediaBridge.RequestAccess();

        foreach (var button in new[] { _previous, _playPause, _next })
        {
            button.WidthRequest = 64;
            button.HeightRequest = 42;
            _controls.Children.Add(button);
        }

        Content = UI.Card(
            UI.Label("♫ YouTube Music – kísérleti vezérlés", 16, true, UI.Purple),
            _track, _artist, _controls, _access, _status);
        Refresh();
    }

    public void Start()
    {
        Refresh();
        if (!_timer.IsRunning) _timer.Start();
    }

    public void Stop() => _timer.Stop();

    private void Send(YouTubeMusicMediaBridge.MusicCommand command)
    {
        if (!YouTubeMusicMediaBridge.TryControl(command))
            _status.Text = "A YouTube Music nem fogadta a vezérlést.";
        else
            Refresh();
    }

    private void Refresh()
    {
        var state = YouTubeMusicMediaBridge.GetSnapshot();
        _access.IsVisible = !state.HasAccess;
        _controls.IsVisible = state.HasSession;
        _track.IsVisible = state.HasSession;
        _artist.IsVisible = state.HasSession && !string.IsNullOrWhiteSpace(state.Artist);
        if (!state.HasAccess)
        {
            _status.Text = "A mini-lejátszóhoz Android-értesítés-hozzáférést kell adnod. " +
                "Ez rendszerszinten más alkalmazások értesítéseihez is hozzáférést biztosít. " +
                "A Mozdulj! kizárólag a YouTube Music aktív médiaállapotát kéri le; " +
                "az engedély bármikor visszavonható.";
            return;
        }
        if (!state.HasSession)
        {
            _status.Text = "Nincs aktív YouTube Music-lejátszás. Indítsd el a listát a YouTube Musicban; " +
                "utána itt vezérelheted a zenét.";
            return;
        }
        _track.Text = string.IsNullOrWhiteSpace(state.Title)
            ? "YouTube Music" : state.Title;
        _artist.Text = state.Artist;
        _playPause.Text = state.Playing ? "⏸" : "▶";
        _previous.IsEnabled = state.CanPrevious;
        _next.IsEnabled = state.CanNext;
        _status.Text = state.Playing
            ? "Lejátszás a YouTube Music alkalmazásban"
            : "Szünetel a YouTube Music lejátszása";
    }
}
