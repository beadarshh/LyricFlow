namespace LyricFlow.Media;

/// <summary>
/// Local playback clock. SMTC only reports position on events, so we extrapolate between them
/// and resync on every update (seek, pause, track change).
/// </summary>
public sealed class PlaybackClock
{
    private readonly Func<DateTimeOffset> _utcNow;
    private TimeSpan _position;
    private DateTimeOffset _stamp;
    private bool _isPlaying;
    private double _rate = 1.0;

    public PlaybackClock(Func<DateTimeOffset>? utcNow = null)
    {
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _stamp = _utcNow();
    }

    public bool IsPlaying => _isPlaying;

    public void Update(TimelineSnapshot snapshot)
    {
        _position = snapshot.Position;
        _stamp = snapshot.UpdatedAt;
        _isPlaying = snapshot.IsPlaying;
        _rate = snapshot.Rate > 0 ? snapshot.Rate : 1.0;
    }

    public TimeSpan Now
    {
        get
        {
            if (!_isPlaying)
                return _position;

            var elapsed = _utcNow() - _stamp;
            if (elapsed < TimeSpan.Zero)
                elapsed = TimeSpan.Zero;

            return _position + TimeSpan.FromTicks((long)(elapsed.Ticks * _rate));
        }
    }
}
