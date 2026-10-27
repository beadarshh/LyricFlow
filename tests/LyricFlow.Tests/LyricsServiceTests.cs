using LyricFlow.Lyrics;
using LyricFlow.Media;

namespace LyricFlow.Tests;

public sealed class LyricsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "LyricFlowTests-" + Guid.NewGuid().ToString("N"));

    private static readonly TrackInfo Track = new("Song (Official Video)", "Artist - Topic", "Album", TimeSpan.FromSeconds(200), "app");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private sealed class FakeSource : ILyricsSource
    {
        public List<LyricsQuery> Queries { get; } = new();

        public Func<LyricsQuery, IReadOnlyList<LyricsCandidate>> Respond { get; set; } = _ => Array.Empty<LyricsCandidate>();

        public Task<IReadOnlyList<LyricsCandidate>> FindAsync(LyricsQuery query, CancellationToken cancellationToken)
        {
            Queries.Add(query);
            return Task.FromResult(Respond(query));
        }
    }

    private LyricsService Create(FakeSource source) => new(source, new LyricsCache(_directory));

    private static LyricsCandidate Synced(string title) =>
        new(title, "Artist", "Album", 200, false, "hi", "[00:01.00]hello there\n[00:05.00]second");

    [Fact]
    public async Task Returns_parsed_synced_lyrics()
    {
        var source = new FakeSource { Respond = q => new[] { Synced(q.Title) } };

        var result = await Create(source).GetAsync(Track, CancellationToken.None);

        Assert.Equal(LyricsKind.Synced, result.Kind);
        Assert.Equal(2, result.Lines.Count);
    }

    [Fact]
    public async Task Retries_with_a_cleaned_title_when_the_raw_one_finds_nothing()
    {
        var source = new FakeSource
        {
            Respond = q => q.Title == "Song" ? new[] { Synced("Song") } : Array.Empty<LyricsCandidate>(),
        };

        var result = await Create(source).GetAsync(Track, CancellationToken.None);

        Assert.Equal(LyricsKind.Synced, result.Kind);
        Assert.Equal(new[] { "Song (Official Video)", "Song" }, source.Queries.Select(q => q.Title));
        Assert.Equal("Artist", source.Queries[1].Artist);
    }

    [Fact]
    public async Task Second_request_is_served_from_the_cache()
    {
        var source = new FakeSource { Respond = q => new[] { Synced(q.Title) } };
        var service = Create(source);

        await service.GetAsync(Track, CancellationToken.None);
        var calls = source.Queries.Count;
        var again = await service.GetAsync(Track, CancellationToken.None);

        Assert.Equal(calls, source.Queries.Count);
        Assert.Equal(LyricsKind.Synced, again.Kind);
    }

    [Fact]
    public async Task No_match_is_reported_and_cached_as_not_found()
    {
        var source = new FakeSource();
        var service = Create(source);

        var first = await service.GetAsync(Track, CancellationToken.None);
        var calls = source.Queries.Count;
        await service.GetAsync(Track, CancellationToken.None);

        Assert.Equal(LyricsKind.NotFound, first.Kind);
        Assert.Equal(calls, source.Queries.Count);
    }

    [Fact]
    public async Task Source_failure_is_an_error_and_is_not_cached()
    {
        var failing = new ThrowingSource();
        var service = new LyricsService(failing, new LyricsCache(_directory));

        var first = await service.GetAsync(Track, CancellationToken.None);
        await service.GetAsync(Track, CancellationToken.None);

        Assert.Equal(LyricsKind.Error, first.Kind);
        Assert.Equal(2, failing.Calls);
    }

    [Fact]
    public async Task Instrumental_candidate_gives_an_instrumental_result()
    {
        var source = new FakeSource
        {
            Respond = _ => new[] { new LyricsCandidate("Song", "Artist", "Album", 200, true, null, null) },
        };

        var result = await Create(source).GetAsync(Track, CancellationToken.None);

        Assert.Equal(LyricsKind.Instrumental, result.Kind);
    }

    [Fact]
    public async Task Plain_only_candidate_gives_a_plain_result()
    {
        var source = new FakeSource
        {
            Respond = _ => new[] { new LyricsCandidate("Song", "Artist", "Album", 200, false, "just words", null) },
        };

        var result = await Create(source).GetAsync(Track, CancellationToken.None);

        Assert.Equal(LyricsKind.Plain, result.Kind);
    }

    private sealed class ThrowingSource : ILyricsSource
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<LyricsCandidate>> FindAsync(LyricsQuery query, CancellationToken cancellationToken)
        {
            Calls++;
            throw new HttpRequestException("offline");
        }
    }
}
