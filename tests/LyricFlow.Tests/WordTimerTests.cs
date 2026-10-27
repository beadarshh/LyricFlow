using LyricFlow.Lyrics;

namespace LyricFlow.Tests;

public class WordTimerTests
{
    private static readonly TimeSpan Start = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan End = TimeSpan.FromSeconds(14);

    [Fact]
    public void Words_are_contiguous_and_cover_the_whole_line()
    {
        var words = WordTimer.Split("never gonna give you up", Start, End);

        Assert.Equal(5, words.Count);
        Assert.Equal(Start, words[0].Start);
        Assert.Equal(End, words[^1].End);
        for (var i = 1; i < words.Count; i++)
            Assert.Equal(words[i - 1].End, words[i].Start);
    }

    [Fact]
    public void Longer_words_get_more_time()
    {
        var words = WordTimer.Split("extraordinarily go", Start, End);

        Assert.True(words[0].End - words[0].Start > words[1].End - words[1].Start);
    }

    [Fact]
    public void Short_words_get_a_minimum_share()
    {
        var words = WordTimer.Split("I am", Start, End);

        // Both fall under the minimum weight, so they split the line evenly.
        Assert.Equal(words[0].End - words[0].Start, words[1].End - words[1].Start);
    }

    [Fact]
    public void Keeps_trailing_space_on_all_but_the_last_word()
    {
        var words = WordTimer.Split("one two", Start, End);

        Assert.Equal("one ", words[0].Text);
        Assert.Equal("two", words[1].Text);
    }

    [Fact]
    public void Splits_unspaced_cjk_text_per_character()
    {
        var words = WordTimer.Split("君の名は", Start, End);

        Assert.Equal(4, words.Count);
    }

    [Fact]
    public void Empty_text_has_no_words()
    {
        Assert.Empty(WordTimer.Split("   ", Start, End));
    }

    [Fact]
    public void Zero_length_line_does_not_throw()
    {
        var words = WordTimer.Split("quick", Start, Start);

        Assert.Single(words);
        Assert.Equal(Start, words[0].End);
    }
}
