using LyricFlow.Lyrics;

namespace LyricFlow.Tests;

public class LyricsMatcherTests
{
    private static readonly LyricsQuery Query = new("Song", "Artist", "Album", TimeSpan.FromSeconds(200));

    private static LyricsCandidate Candidate(
        double duration,
        string album = "Album",
        string? synced = "[00:01.00]x",
        string? plain = "x",
        bool instrumental = false) =>
        new("Song", "Artist", album, duration, instrumental, plain, synced);

    [Fact]
    public void Picks_the_version_closest_to_the_track_duration()
    {
        var far = Candidate(205);
        var near = Candidate(201);

        Assert.Same(near, LyricsMatcher.Pick(new[] { far, near }, Query));
    }

    [Fact]
    public void Synced_beats_plain_within_tolerance()
    {
        var plain = Candidate(200, synced: null);
        var synced = Candidate(201);

        Assert.Same(synced, LyricsMatcher.Pick(new[] { plain, synced }, Query));
    }

    [Fact]
    public void Matching_album_breaks_ties()
    {
        var other = Candidate(200, album: "Greatest Hits");
        var same = Candidate(201);

        Assert.Same(same, LyricsMatcher.Pick(new[] { other, same }, Query));
    }

    [Fact]
    public void Falls_back_to_closest_synced_when_nothing_is_within_tolerance()
    {
        var off = Candidate(206);

        Assert.Same(off, LyricsMatcher.Pick(new[] { off }, Query));
    }

    [Fact]
    public void Rejects_wildly_different_versions()
    {
        Assert.Null(LyricsMatcher.Pick(new[] { Candidate(260) }, Query));
    }

    [Fact]
    public void Instrumental_flag_is_a_valid_match()
    {
        var instrumental = Candidate(200, synced: null, plain: null, instrumental: true);

        Assert.Same(instrumental, LyricsMatcher.Pick(new[] { instrumental }, Query));
    }

    [Fact]
    public void Candidates_with_nothing_to_show_are_ignored()
    {
        var empty = Candidate(200, synced: null, plain: null);

        Assert.Null(LyricsMatcher.Pick(new[] { empty }, Query));
    }

    [Fact]
    public void Unknown_duration_prefers_synced()
    {
        var query = Query with { Duration = TimeSpan.Zero };
        var plain = Candidate(180, synced: null);
        var synced = Candidate(300);

        Assert.Same(synced, LyricsMatcher.Pick(new[] { plain, synced }, query));
    }
}
