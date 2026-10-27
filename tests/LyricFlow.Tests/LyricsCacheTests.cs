using LyricFlow.Lyrics;

namespace LyricFlow.Tests;

public sealed class LyricsCacheTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "LyricFlowTests-" + Guid.NewGuid().ToString("N"));
    private DateTimeOffset _now = T0;

    private LyricsCache CreateCache() => new(_directory, () => _now, TimeSpan.FromHours(6));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Round_trips_an_entry()
    {
        var cache = CreateCache();
        var entry = new CacheEntry(LyricsKind.Synced, "[00:01.00]hi", "hi", T0);

        cache.Put("abc", entry);

        Assert.Equal(entry, cache.TryGet("abc"));
    }

    [Fact]
    public void Missing_key_returns_null()
    {
        Assert.Null(CreateCache().TryGet("nope"));
    }

    [Fact]
    public void Not_found_entries_expire()
    {
        var cache = CreateCache();
        cache.Put("k", new CacheEntry(LyricsKind.NotFound, null, null, T0));

        _now = T0 + TimeSpan.FromHours(5);
        Assert.NotNull(cache.TryGet("k"));

        _now = T0 + TimeSpan.FromHours(7);
        Assert.Null(cache.TryGet("k"));
    }

    [Fact]
    public void Found_entries_never_expire()
    {
        var cache = CreateCache();
        cache.Put("k", new CacheEntry(LyricsKind.Synced, "[00:01.00]hi", null, T0));

        _now = T0 + TimeSpan.FromDays(365);

        Assert.NotNull(cache.TryGet("k"));
    }

    [Fact]
    public void Corrupt_file_is_treated_as_a_miss()
    {
        var cache = CreateCache();
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "bad.json"), "{ not json");

        Assert.Null(cache.TryGet("bad"));
    }

    [Fact]
    public void Key_ignores_case_and_small_duration_differences()
    {
        var a = LyricsCache.KeyFor(new LyricsQuery("Song", "Artist", "", TimeSpan.FromSeconds(200)));
        var b = LyricsCache.KeyFor(new LyricsQuery("  song ", "ARTIST", "Other album", TimeSpan.FromSeconds(200.4)));
        var c = LyricsCache.KeyFor(new LyricsQuery("Song", "Artist", "", TimeSpan.FromSeconds(260)));

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
