namespace LyricFlow.Lyrics;

/// <summary>A place lyrics can be looked up. Throws on transport failures; returns an empty list when nothing matches.</summary>
public interface ILyricsSource
{
    Task<IReadOnlyList<LyricsCandidate>> FindAsync(LyricsQuery query, CancellationToken cancellationToken);
}
