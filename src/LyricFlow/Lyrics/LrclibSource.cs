using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LyricFlow.Lyrics;

public sealed class LrclibSource : ILyricsSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public LrclibSource(HttpClient http)
    {
        _http = http;
    }

    public static HttpClient CreateHttpClient()
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri("https://lrclib.net/"),
            Timeout = TimeSpan.FromSeconds(10),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("LyricFlow/0.1");
        return http;
    }

    public async Task<IReadOnlyList<LyricsCandidate>> FindAsync(LyricsQuery query, CancellationToken cancellationToken)
    {
        if (query.Duration > TimeSpan.Zero && !string.IsNullOrWhiteSpace(query.Album))
        {
            var exact = await GetExactAsync(query, cancellationToken);
            if (exact is not null)
                return new[] { exact };
        }

        var results = await SearchAsync(
            $"api/search?track_name={Uri.EscapeDataString(query.Title)}&artist_name={Uri.EscapeDataString(query.Artist)}",
            cancellationToken);
        if (results.Count > 0)
            return results;

        return await SearchAsync(
            $"api/search?q={Uri.EscapeDataString($"{query.Title} {query.Artist}".Trim())}",
            cancellationToken);
    }

    private async Task<LyricsCandidate?> GetExactAsync(LyricsQuery query, CancellationToken cancellationToken)
    {
        var seconds = (int)Math.Round(query.Duration.TotalSeconds);
        var url = $"api/get?track_name={Uri.EscapeDataString(query.Title)}"
            + $"&artist_name={Uri.EscapeDataString(query.Artist)}"
            + $"&album_name={Uri.EscapeDataString(query.Album)}"
            + $"&duration={seconds}";

        using var response = await _http.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<LrclibDto>(JsonOptions, cancellationToken);
        return dto?.ToCandidate();
    }

    private async Task<IReadOnlyList<LyricsCandidate>> SearchAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return Array.Empty<LyricsCandidate>();

        response.EnsureSuccessStatusCode();
        var dtos = await response.Content.ReadFromJsonAsync<List<LrclibDto>>(JsonOptions, cancellationToken);
        return dtos?.Select(d => d.ToCandidate()).ToList() ?? new List<LyricsCandidate>();
    }

    private sealed class LrclibDto
    {
        [JsonPropertyName("trackName")] public string? TrackName { get; set; }

        [JsonPropertyName("artistName")] public string? ArtistName { get; set; }

        [JsonPropertyName("albumName")] public string? AlbumName { get; set; }

        [JsonPropertyName("duration")] public double? Duration { get; set; }

        [JsonPropertyName("instrumental")] public bool? Instrumental { get; set; }

        [JsonPropertyName("plainLyrics")] public string? PlainLyrics { get; set; }

        [JsonPropertyName("syncedLyrics")] public string? SyncedLyrics { get; set; }

        public LyricsCandidate ToCandidate() => new(
            TrackName ?? string.Empty,
            ArtistName ?? string.Empty,
            AlbumName ?? string.Empty,
            Duration ?? 0,
            Instrumental ?? false,
            PlainLyrics,
            SyncedLyrics);
    }
}
