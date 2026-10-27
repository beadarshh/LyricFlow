using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LyricFlow.Lyrics;

/// <summary>What we remember about a lookup. <see cref="Kind"/> is never <see cref="LyricsKind.Error"/>.</summary>
public sealed record CacheEntry(LyricsKind Kind, string? SyncedLyrics, string? PlainLyrics, DateTimeOffset FetchedAt);

/// <summary>One JSON file per track under the cache directory. "Not found" entries expire so new uploads are picked up.</summary>
public sealed class LyricsCache
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _directory;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly TimeSpan _notFoundTtl;

    public LyricsCache(string directory, Func<DateTimeOffset>? utcNow = null, TimeSpan? notFoundTtl = null)
    {
        _directory = directory;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _notFoundTtl = notFoundTtl ?? TimeSpan.FromHours(6);
    }

    public static string KeyFor(LyricsQuery query)
    {
        // Duration is bucketed so a second of disagreement between players doesn't miss the cache.
        var bucket = (int)Math.Round(query.Duration.TotalSeconds / 3);
        var normalized = $"{query.Title.Trim().ToLowerInvariant()}|{query.Artist.Trim().ToLowerInvariant()}|{bucket}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16];
    }

    public CacheEntry? TryGet(string key)
    {
        try
        {
            var path = PathFor(key);
            if (!File.Exists(path))
                return null;

            var entry = JsonSerializer.Deserialize<CacheEntry>(File.ReadAllText(path), JsonOptions);
            if (entry is null)
                return null;

            if (entry.Kind == LyricsKind.NotFound && _utcNow() - entry.FetchedAt > _notFoundTtl)
                return null;

            return entry;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Lyrics cache read failed: {ex.Message}");
            return null;
        }
    }

    public void Put(string key, CacheEntry entry)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(PathFor(key), JsonSerializer.Serialize(entry, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Lyrics cache write failed: {ex.Message}");
        }
    }

    private string PathFor(string key) => Path.Combine(_directory, key + ".json");
}
