using LyricFlow.Lyrics;

namespace LyricFlow.Tests;

public class LrcParserTests
{
    [Fact]
    public void Parses_timestamps_and_text()
    {
        var lines = LrcParser.Parse("[00:12.50] Hello world\n[00:15.00] Second line");

        Assert.Equal(2, lines.Count);
        Assert.Equal(TimeSpan.FromSeconds(12.5), lines[0].Start);
        Assert.Equal("Hello world", lines[0].Text);
        Assert.Equal(TimeSpan.FromSeconds(15), lines[1].Start);
    }

    [Fact]
    public void Line_ends_when_the_next_starts_but_is_capped()
    {
        var lines = LrcParser.Parse("[00:10.00]Short\n[00:12.00]Long gap after\n[01:00.00]Late");

        Assert.Equal(TimeSpan.FromSeconds(12), lines[0].End);
        Assert.Equal(TimeSpan.FromSeconds(12) + LrcParser.MaxLineDuration, lines[1].End);
        Assert.Equal(TimeSpan.FromSeconds(60) + LrcParser.LastLineDuration, lines[2].End);
    }

    [Fact]
    public void Expands_multiple_timestamps_on_one_line_and_sorts()
    {
        var lines = LrcParser.Parse("[00:30.00][00:10.00]Chorus\n[00:20.00]Verse");

        Assert.Equal(new[] { "Chorus", "Verse", "Chorus" }, lines.Select(l => l.Text));
        Assert.Equal(new[] { 10.0, 20.0, 30.0 }, lines.Select(l => l.Start.TotalSeconds));
    }

    [Fact]
    public void Blank_lyric_becomes_a_gap_without_words()
    {
        var lines = LrcParser.Parse("[00:10.00]Sing\n[00:14.00]\n[00:20.00]Again");

        Assert.True(lines[1].IsGap);
        Assert.Empty(lines[1].Words);
    }

    [Fact]
    public void Positive_offset_shifts_lyrics_earlier()
    {
        var lines = LrcParser.Parse("[offset:+500]\n[00:10.00]Line");

        Assert.Equal(TimeSpan.FromSeconds(9.5), lines[0].Start);
    }

    [Fact]
    public void Ignores_metadata_and_malformed_lines()
    {
        var lines = LrcParser.Parse("[ar:Someone]\n[ti:Something]\nnot a lyric\n[bad]\n[00:05.00]Real");

        Assert.Single(lines);
        Assert.Equal("Real", lines[0].Text);
    }

    [Fact]
    public void Strips_enhanced_word_tags()
    {
        var lines = LrcParser.Parse("[00:10.00]<00:10.00>Hello <00:10.50>there");

        Assert.Equal("Hello there", lines[0].Text);
    }

    [Fact]
    public void Handles_windows_line_endings_and_two_digit_fractions()
    {
        var lines = LrcParser.Parse("[00:01.5]A\r\n[00:02.25]B\r\n");

        Assert.Equal(TimeSpan.FromSeconds(1.5), lines[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(2.25), lines[1].Start);
        Assert.Equal("B", lines[1].Text);
    }

    [Fact]
    public void Empty_input_yields_no_lines()
    {
        Assert.Empty(LrcParser.Parse(string.Empty));
    }

    [Fact]
    public void IndexAt_finds_the_last_started_line()
    {
        var result = LyricsResult.Synced(LrcParser.Parse("[00:10.00]A\n[00:20.00]B\n[00:30.00]C"));

        Assert.Equal(-1, result.IndexAt(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, result.IndexAt(TimeSpan.FromSeconds(10)));
        Assert.Equal(1, result.IndexAt(TimeSpan.FromSeconds(25)));
        Assert.Equal(2, result.IndexAt(TimeSpan.FromMinutes(5)));
    }
}
