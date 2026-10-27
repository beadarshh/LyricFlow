namespace LyricFlow.Lyrics;

/// <summary>A word with its (possibly estimated) timing. <see cref="Text"/> keeps the trailing space, if any.</summary>
public sealed record LyricWord(string Text, TimeSpan Start, TimeSpan End);

/// <summary>
/// One timed lyric line. <see cref="End"/> is when the line has been fully sung; the line stays
/// on screen until the next line starts. An empty <see cref="Text"/> is an instrumental gap.
/// </summary>
public sealed record LyricLine(TimeSpan Start, TimeSpan End, string Text, IReadOnlyList<LyricWord> Words)
{
    public bool IsGap => Text.Length == 0;
}

public enum LyricsKind
{
    Synced,
    Plain,
    Instrumental,
    NotFound,

    /// <summary>Lookup failed (offline, server error). Never cached.</summary>
    Error,
}

public sealed class LyricsResult
{
    private LyricsResult(LyricsKind kind, IReadOnlyList<LyricLine> lines)
    {
        Kind = kind;
        Lines = lines;
    }

    public LyricsKind Kind { get; }

    public IReadOnlyList<LyricLine> Lines { get; }

    public static LyricsResult Synced(IReadOnlyList<LyricLine> lines) => new(LyricsKind.Synced, lines);

    public static LyricsResult Of(LyricsKind kind) => new(kind, Array.Empty<LyricLine>());

    /// <summary>Index of the last line that has started at <paramref name="position"/>, or -1 before the first line.</summary>
    public int IndexAt(TimeSpan position)
    {
        int lo = 0, hi = Lines.Count - 1, result = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (Lines[mid].Start <= position)
            {
                result = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return result;
    }
}

/// <summary>Raw lyrics as returned by a source, before parsing.</summary>
public sealed record LyricsCandidate(
    string TrackName,
    string ArtistName,
    string AlbumName,
    double DurationSeconds,
    bool Instrumental,
    string? PlainLyrics,
    string? SyncedLyrics)
{
    public bool HasSynced => !string.IsNullOrWhiteSpace(SyncedLyrics);

    public bool HasPlain => !string.IsNullOrWhiteSpace(PlainLyrics);
}

public sealed record LyricsQuery(string Title, string Artist, string Album, TimeSpan Duration);
