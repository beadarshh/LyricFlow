using LyricFlow.Lyrics;

namespace LyricFlow.Tests;

public class TrackNormalizerTests
{
    [Theory]
    [InlineData("Song (Official Video)", "Song")]
    [InlineData("Song [Official Audio]", "Song")]
    [InlineData("Song (Lyrics)", "Song")]
    [InlineData("Song (feat. Someone)", "Song")]
    [InlineData("Song - 2011 Remaster", "Song")]
    [InlineData("Song - Remastered", "Song")]
    [InlineData("Song (Live at Wembley)", "Song (Live at Wembley)")]
    [InlineData("Plain Title", "Plain Title")]
    public void Cleans_titles(string input, string expected)
    {
        Assert.Equal(expected, TrackNormalizer.CleanTitle(input));
    }

    [Theory]
    [InlineData("Artist - Topic", "Artist")]
    [InlineData("Main; Guest", "Main")]
    [InlineData("Tyler, The Creator", "Tyler, The Creator")]
    public void Cleans_artists(string input, string expected)
    {
        Assert.Equal(expected, TrackNormalizer.CleanArtist(input));
    }
}
