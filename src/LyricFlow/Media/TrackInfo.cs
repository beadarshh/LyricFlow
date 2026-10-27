namespace LyricFlow.Media;

public sealed record TrackInfo(string Title, string Artist, string Album, TimeSpan Duration, string SourceApp)
{
    public string IdentityKey => $"{Title}|{Artist}|{Album}";
}

public readonly record struct TimelineSnapshot(TimeSpan Position, DateTimeOffset UpdatedAt, bool IsPlaying, double Rate);
