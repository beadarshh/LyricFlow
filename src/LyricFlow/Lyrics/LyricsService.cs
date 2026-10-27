using System.Diagnostics;
using LyricFlow.Media;

namespace LyricFlow.Lyrics;

/// <summary>cache → source → version match → parse.</summary>
public sealed class LyricsService
{
    private readonly ILyricsSource _source;
    private readonly LyricsCache _cache;
    private readonly Func<DateTimeOffset> _utcNow;

    public LyricsService(ILyricsSource source, LyricsCache cache, Func<DateTimeOffset>? utcNow = null)
    {
        _source = source;
        _cache = cache;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<LyricsResult> GetAsync(TrackInfo track, CancellationToken cancellationToken)
    {
        var query = new LyricsQuery(track.Title, track.Artist, track.Album, track.Duration);
        var key = LyricsCache.KeyFor(query);

        var cached = _cache.TryGet(key);
        if (cached is not null)
            return Build(cached);

        try
        {
            var pick = await FindBestAsync(query, cancellationToken);
            var entry = ToEntry(pick);
            _cache.Put(key, entry);
            return Build(entry);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Lyrics lookup failed: {ex.Message}");
            return LyricsResult.Of(LyricsKind.Error);
        }
    }

    private async Task<LyricsCandidate?> FindBestAsync(LyricsQuery query, CancellationToken cancellationToken)
    {
        foreach (var variant in Variants(query))
        {
            var candidates = await _source.FindAsync(variant, cancellationToken);
            var pick = LyricsMatcher.Pick(candidates, variant);
            if (pick is not null)
                return pick;
        }

        return null;
    }

    private static IEnumerable<LyricsQuery> Variants(LyricsQuery query)
    {
        yield return query;

        var cleaned = query with
        {
            Title = TrackNormalizer.CleanTitle(query.Title),
            Artist = TrackNormalizer.CleanArtist(query.Artist),
        };
        if (cleaned != query && cleaned.Title.Length > 0)
            yield return cleaned;
    }

    private CacheEntry ToEntry(LyricsCandidate? pick)
    {
        var now = _utcNow();
        if (pick is null)
            return new CacheEntry(LyricsKind.NotFound, null, null, now);
        if (pick.HasSynced)
            return new CacheEntry(LyricsKind.Synced, pick.SyncedLyrics, pick.PlainLyrics, now);
        if (pick.HasPlain)
            return new CacheEntry(LyricsKind.Plain, null, pick.PlainLyrics, now);
        return new CacheEntry(pick.Instrumental ? LyricsKind.Instrumental : LyricsKind.NotFound, null, null, now);
    }

    private static LyricsResult Build(CacheEntry entry)
    {
        switch (entry.Kind)
        {
            case LyricsKind.Synced:
                var lines = LrcParser.Parse(entry.SyncedLyrics ?? string.Empty);
                return lines.Any(l => !l.IsGap)
                    ? LyricsResult.Synced(lines)
                    : LyricsResult.Of(string.IsNullOrWhiteSpace(entry.PlainLyrics) ? LyricsKind.NotFound : LyricsKind.Plain);
            case LyricsKind.Plain:
            case LyricsKind.Instrumental:
                return LyricsResult.Of(entry.Kind);
            default:
                return LyricsResult.Of(LyricsKind.NotFound);
        }
    }
}
