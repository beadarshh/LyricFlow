using System.Windows.Threading;
using LyricFlow.Diagnostics;
using Windows.Media.Control;

namespace LyricFlow.Media;

/// <summary>Listens to the System Media Transport Controls and raises track / timeline events on the UI thread.</summary>
public sealed class MediaSessionService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private TrackInfo? _track;
    private int _refreshVersion;

    public MediaSessionService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>Null when nothing usable is playing.</summary>
    public event Action<TrackInfo?>? TrackChanged;

    public event Action<TimelineSnapshot>? TimelineChanged;

    public async Task StartAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.CurrentSessionChanged += (_, _) => Post(AttachSession);
        AppLog.Write("Media controls ready.");
        AttachSession();
    }

    private void Post(Action action) => _dispatcher.BeginInvoke(action);

    private void AttachSession()
    {
        DetachSession();

        try
        {
            _session = _manager?.GetCurrentSession();
            if (_session is null)
            {
                AppLog.Write("No media session.");
                SetTrack(null);
                return;
            }

            AppLog.Write($"Media session: {_session.SourceAppUserModelId}");
            _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            _session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
        }
        catch (Exception ex)
        {
            AppLog.Write($"Media session attach failed: {ex}");
            SetTrack(null);
            return;
        }

        _ = RefreshMediaAsync();
        PublishTimeline();
    }

    private void DetachSession()
    {
        if (_session is null)
            return;

        _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        _session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        _session = null;
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession s, object _) =>
        Post(() =>
        {
            if (!ReferenceEquals(s, _session)) return;
            _ = RefreshMediaAsync();
            PublishTimeline();
        });

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession s, object _) =>
        Post(() =>
        {
            if (ReferenceEquals(s, _session)) PublishTimeline();
        });

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession s, object _) =>
        Post(() =>
        {
            if (ReferenceEquals(s, _session)) PublishTimeline();
        });

    private async Task RefreshMediaAsync()
    {
        var session = _session;
        if (session is null)
            return;

        var version = ++_refreshVersion;

        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            if (version != _refreshVersion || !ReferenceEquals(session, _session))
                return;

            if (props is null || string.IsNullOrWhiteSpace(props.Title))
            {
                SetTrack(null);
                return;
            }

            var duration = ReadDuration(session);
            var track = new TrackInfo(
                props.Title.Trim(),
                (props.Artist ?? string.Empty).Trim(),
                (props.AlbumTitle ?? string.Empty).Trim(),
                duration,
                session.SourceAppUserModelId ?? string.Empty);

            // MediaPropertiesChanged fires repeatedly for the same track (e.g. when artwork loads).
            if (_track is not null && _track.IdentityKey == track.IdentityKey && _track.Duration == track.Duration)
                return;

            SetTrack(track);
        }
        catch (Exception ex)
        {
            AppLog.Write($"Media refresh failed: {ex}");
        }
    }

    private static TimeSpan ReadDuration(GlobalSystemMediaTransportControlsSession session)
    {
        var timeline = session.GetTimelineProperties();
        var duration = timeline.EndTime - timeline.StartTime;
        return duration > TimeSpan.Zero ? duration : TimeSpan.Zero;
    }

    private void PublishTimeline()
    {
        var session = _session;
        if (session is null)
            return;

        try
        {
            var timeline = session.GetTimelineProperties();
            var info = session.GetPlaybackInfo();

            var updatedAt = timeline.LastUpdatedTime;
            if (updatedAt.Year < 2000)
                updatedAt = DateTimeOffset.UtcNow;

            var playing = info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            TimelineChanged?.Invoke(new TimelineSnapshot(timeline.Position, updatedAt, playing, info.PlaybackRate ?? 1.0));

            // Some players report the duration a moment after the title.
            var duration = ReadDuration(session);
            if (_track is not null && _track.Duration == TimeSpan.Zero && duration > TimeSpan.Zero)
                SetTrack(_track with { Duration = duration });
        }
        catch (Exception ex)
        {
            AppLog.Write($"Timeline read failed: {ex}");
        }
    }

    private void SetTrack(TrackInfo? track)
    {
        _track = track;
        TrackChanged?.Invoke(track);
    }

    public void Dispose() => DetachSession();
}
