using System.Windows;
using LyricFlow.Diagnostics;
using LyricFlow.Lyrics;
using LyricFlow.Media;
using LyricFlow.Settings;
using LyricFlow.UI;

namespace LyricFlow;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private SettingsStore _settings = null!;
    private PlaybackClock _clock = null!;
    private OverlayWindow _overlay = null!;
    private MediaSessionService _media = null!;
    private LyricsService _lyrics = null!;
    private TrayService _tray = null!;
    private HotkeyService _hotkey = null!;
    private HttpClient _http = null!;
    private SettingsWindow? _settingsWindow;
    private CancellationTokenSource? _lookup;
    private string? _loadedKey;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // An overlay should survive a stray exception on the UI thread; record it so it can be diagnosed.
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Write($"Unhandled error: {args.Exception}");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => AppLog.Write($"Fatal error: {args.ExceptionObject}");

        _singleInstance = new Mutex(true, @"Local\LyricFlow.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        _settings = SettingsStore.Load(SettingsStore.DefaultPath);
        _clock = new PlaybackClock();

        var cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LyricFlow", "cache");
        _http = LrclibSource.CreateHttpClient();
        _lyrics = new LyricsService(new LrclibSource(_http), new LyricsCache(cacheDirectory));

        _overlay = new OverlayWindow(_clock, _settings);
        _overlay.IsVisibleChanged += (_, _) => _tray?.SetOverlayVisible(_overlay.IsVisible);
        if (_settings.Current.Visible)
            _overlay.Present(centerOnShow: false);

        _hotkey = new HotkeyService();
        _hotkey.Pressed += () => ToggleOverlay(_settings.Current.CenterOnHotkey);
        _hotkey.Register(_settings.Current.Hotkey);

        _tray = new TrayService();
        _tray.SetOverlayVisible(_overlay.IsVisible);
        _tray.ToggleVisibleRequested += () => ToggleOverlay(false);
        _tray.MoveModeRequested += SetMoveMode;
        _tray.SettingsRequested += ShowSettings;
        _tray.QuitRequested += Quit;

        _media = new MediaSessionService(Dispatcher);
        _media.TimelineChanged += OnTimelineChanged;
        _media.TrackChanged += OnTrackChanged;

        try
        {
            await _media.StartAsync();
        }
        catch (Exception ex)
        {
            AppLog.Write($"Could not start media session service: {ex}");
        }
    }

    private void OnTimelineChanged(TimelineSnapshot snapshot)
    {
        _clock.Update(snapshot);
        _overlay.SetPlaying(snapshot.IsPlaying);
    }

    private async void OnTrackChanged(TrackInfo? track)
    {
        _lookup?.Cancel();
        _lookup = null;

        if (track is null)
        {
            _loadedKey = null;
            _overlay.SetSession(false);
            _overlay.SetLyrics(null);
            return;
        }

        // The duration often arrives after the title; only a changed key is worth a new lookup.
        var key = $"{track.IdentityKey}|{track.Duration > TimeSpan.Zero}";
        if (key == _loadedKey)
            return;

        _loadedKey = key;
        _overlay.SetSession(true);
        _overlay.SetLyrics(null);
        AppLog.Write($"Track: {track.Title} - {track.Artist} ({track.Duration.TotalSeconds:0}s) via {track.SourceApp}");

        var cts = _lookup = new CancellationTokenSource();
        try
        {
            var result = await _lyrics.GetAsync(track, cts.Token);
            if (!cts.IsCancellationRequested)
            {
                AppLog.Write($"Lyrics: {result.Kind} ({result.Lines.Count} lines)");
                _overlay.SetLyrics(result);
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer track.
        }
    }

    private void ToggleOverlay(bool centerOnShow)
    {
        _overlay.ToggleVisible(centerOnShow);
        _tray.SetOverlayVisible(_overlay.IsVisible);
    }

    private void SetMoveMode(bool on)
    {
        _overlay.SetMoveMode(on);
        _tray.SetOverlayVisible(_overlay.IsVisible);
        _tray.SetMoveMode(_overlay.MoveMode);
    }

    private void ShowSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings, _hotkey, _overlay.ResetPosition);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void Quit()
    {
        _lookup?.Cancel();
        _settings.Save();
        _tray.Dispose();
        _hotkey.Dispose();
        _media.Dispose();
        _http.Dispose();
        _overlay.Close();
        Shutdown();
    }
}
