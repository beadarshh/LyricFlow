using LyricFlow.Media;

namespace LyricFlow.Tests;

public class PlaybackClockTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static (PlaybackClock Clock, Action<TimeSpan> Advance) Create()
    {
        var now = T0;
        return (new PlaybackClock(() => now), by => now += by);
    }

    [Fact]
    public void Extrapolates_while_playing()
    {
        var (clock, advance) = Create();
        clock.Update(new TimelineSnapshot(TimeSpan.FromSeconds(30), T0, true, 1.0));

        advance(TimeSpan.FromSeconds(2));

        Assert.Equal(TimeSpan.FromSeconds(32), clock.Now);
    }

    [Fact]
    public void Holds_position_while_paused()
    {
        var (clock, advance) = Create();
        clock.Update(new TimelineSnapshot(TimeSpan.FromSeconds(30), T0, false, 1.0));

        advance(TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.FromSeconds(30), clock.Now);
    }

    [Fact]
    public void Resyncs_on_seek()
    {
        var (clock, advance) = Create();
        clock.Update(new TimelineSnapshot(TimeSpan.FromSeconds(30), T0, true, 1.0));
        advance(TimeSpan.FromSeconds(5));

        clock.Update(new TimelineSnapshot(TimeSpan.FromSeconds(100), T0 + TimeSpan.FromSeconds(5), true, 1.0));
        advance(TimeSpan.FromSeconds(1));

        Assert.Equal(TimeSpan.FromSeconds(101), clock.Now);
    }

    [Fact]
    public void Honours_playback_rate()
    {
        var (clock, advance) = Create();
        clock.Update(new TimelineSnapshot(TimeSpan.Zero, T0, true, 2.0));

        advance(TimeSpan.FromSeconds(3));

        Assert.Equal(TimeSpan.FromSeconds(6), clock.Now);
    }

    [Fact]
    public void Ignores_a_snapshot_timestamp_in_the_future()
    {
        var (clock, _) = Create();
        clock.Update(new TimelineSnapshot(TimeSpan.FromSeconds(30), T0 + TimeSpan.FromSeconds(5), true, 1.0));

        Assert.Equal(TimeSpan.FromSeconds(30), clock.Now);
    }

    [Fact]
    public void Falls_back_to_normal_speed_for_a_bad_rate()
    {
        var (clock, advance) = Create();
        clock.Update(new TimelineSnapshot(TimeSpan.Zero, T0, true, 0));

        advance(TimeSpan.FromSeconds(2));

        Assert.Equal(TimeSpan.FromSeconds(2), clock.Now);
    }
}
