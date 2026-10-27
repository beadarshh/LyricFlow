using System.Net;
using LyricFlow.Lyrics;

namespace LyricFlow.Tests;

public class LrclibSourceTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = new();

        public Func<string, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            Requests.Add(path);
            return Task.FromResult(Respond(path));
        }
    }

    private static LrclibSource Create(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://lrclib.test/") });

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    private static readonly LyricsQuery Query = new("Song & Dance", "Artist", "Album", TimeSpan.FromSeconds(200.4));

    [Fact]
    public async Task Exact_match_is_returned_without_searching()
    {
        var handler = new StubHandler
        {
            Respond = _ => Json("""{"trackName":"Song","artistName":"Artist","albumName":"Album","duration":200,"instrumental":false,"plainLyrics":"p","syncedLyrics":"[00:01.00]s"}"""),
        };

        var results = await Create(handler).FindAsync(Query, CancellationToken.None);

        var single = Assert.Single(results);
        Assert.Equal("[00:01.00]s", single.SyncedLyrics);
        var request = Assert.Single(handler.Requests);
        Assert.StartsWith("/api/get?", request);
        Assert.Contains("track_name=Song%20%26%20Dance", request);
        Assert.Contains("duration=200", request);
    }

    [Fact]
    public async Task Falls_back_to_search_when_exact_lookup_is_404()
    {
        var handler = new StubHandler
        {
            Respond = path => path.StartsWith("/api/search?track_name", StringComparison.Ordinal)
                ? Json("""[{"trackName":"A","artistName":"B","albumName":"C","duration":199,"instrumental":true,"plainLyrics":null,"syncedLyrics":null}]""")
                : new HttpResponseMessage(HttpStatusCode.NotFound),
        };

        var results = await Create(handler).FindAsync(Query, CancellationToken.None);

        var single = Assert.Single(results);
        Assert.True(single.Instrumental);
        Assert.Equal(199, single.DurationSeconds);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Uses_free_text_search_as_a_last_resort()
    {
        var handler = new StubHandler
        {
            Respond = path => path.StartsWith("/api/search?q=", StringComparison.Ordinal)
                ? Json("""[{"trackName":"A","artistName":"B","albumName":"C","duration":200,"syncedLyrics":"[00:01.00]x"}]""")
                : path.StartsWith("/api/search?track_name", StringComparison.Ordinal)
                    ? Json("[]")
                    : new HttpResponseMessage(HttpStatusCode.NotFound),
        };

        var results = await Create(handler).FindAsync(Query, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task Skips_exact_lookup_without_a_duration()
    {
        var handler = new StubHandler { Respond = _ => Json("[]") };

        await Create(handler).FindAsync(Query with { Duration = TimeSpan.Zero }, CancellationToken.None);

        Assert.DoesNotContain(handler.Requests, r => r.StartsWith("/api/get", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Server_errors_surface_as_exceptions()
    {
        var handler = new StubHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.InternalServerError) };

        await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler).FindAsync(Query, CancellationToken.None));
    }
}
