namespace LyricFlow.Lyrics;

/// <summary>Chooses the best lyric version among several matches, preferring the one that fits the track's duration.</summary>
public static class LyricsMatcher
{
    public const double ToleranceSeconds = 2.0;

    /// <summary>Used when nothing is within tolerance: the closest synced version, if it is not wildly off.</summary>
    public const double FallbackToleranceSeconds = 10.0;

    public static LyricsCandidate? Pick(IEnumerable<LyricsCandidate> candidates, LyricsQuery query)
    {
        var usable = candidates.Where(c => c.Instrumental || c.HasSynced || c.HasPlain).ToList();
        if (usable.Count == 0)
            return null;

        if (query.Duration <= TimeSpan.Zero)
            return usable.OrderBy(c => Rank(c)).ThenBy(c => AlbumMatches(c, query) ? 0 : 1).First();

        var trackSeconds = query.Duration.TotalSeconds;
        double Delta(LyricsCandidate c) => Math.Abs(c.DurationSeconds - trackSeconds);

        var within = usable
            .Where(c => Delta(c) <= ToleranceSeconds)
            .OrderBy(c => Rank(c))
            .ThenBy(c => AlbumMatches(c, query) ? 0 : 1)
            .ThenBy(Delta)
            .FirstOrDefault();
        if (within is not null)
            return within;

        return usable
            .Where(c => c.HasSynced && Delta(c) <= FallbackToleranceSeconds)
            .OrderBy(Delta)
            .FirstOrDefault();
    }

    // Synced beats plain beats instrumental-only.
    private static int Rank(LyricsCandidate c) => c.HasSynced ? 0 : c.HasPlain ? 1 : 2;

    private static bool AlbumMatches(LyricsCandidate c, LyricsQuery query) =>
        !string.IsNullOrWhiteSpace(query.Album)
        && string.Equals(c.AlbumName?.Trim(), query.Album.Trim(), StringComparison.OrdinalIgnoreCase);
}
