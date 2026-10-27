using LyricFlow.Lyrics;
using LyricFlow.UI;

namespace LyricFlow.Tests;

public class KineticMathTests
{
    private static IReadOnlyList<LyricWord> Words(params double[] startSeconds) =>
        startSeconds.Select(s => new LyricWord("w", TimeSpan.FromSeconds(s), TimeSpan.FromSeconds(s + 0.5))).ToList();

    [Fact]
    public void Active_word_is_the_last_one_started()
    {
        var words = Words(10, 11, 12);

        Assert.Equal(-1, KineticMath.ActiveWordIndex(words, TimeSpan.FromSeconds(9.9)));
        Assert.Equal(0, KineticMath.ActiveWordIndex(words, TimeSpan.FromSeconds(10)));
        Assert.Equal(1, KineticMath.ActiveWordIndex(words, TimeSpan.FromSeconds(11.9)));
        Assert.Equal(2, KineticMath.ActiveWordIndex(words, TimeSpan.FromSeconds(60)));
    }

    [Theory]
    [InlineData(0, 5, 3, 0, 0, 3)]
    [InlineData(2, 5, 3, 0, 2, 3)]
    [InlineData(3, 5, 3, 1, 0, 2)]
    [InlineData(4, 5, 3, 1, 1, 2)]
    [InlineData(0, 1, 3, 0, 0, 1)]
    [InlineData(6, 7, 3, 2, 0, 1)]
    [InlineData(3, 7, 2, 1, 1, 2)]
    [InlineData(6, 7, 1, 6, 0, 1)]
    [InlineData(4, 7, 5, 0, 4, 5)]
    public void Words_are_paged_by_the_configured_size(int index, int count, int perPage, int page, int slot, int pageSize)
    {
        Assert.Equal((page, slot, pageSize), KineticMath.Locate(index, count, perPage));
    }

    [Fact]
    public void Words_per_page_is_kept_in_range()
    {
        Assert.Equal(KineticMath.Locate(2, 9, KineticMath.MaxWordsPerPage), KineticMath.Locate(2, 9, 99));
        Assert.Equal(KineticMath.Locate(2, 9, KineticMath.MinWordsPerPage), KineticMath.Locate(2, 9, 0));
    }

    [Fact]
    public void Stage_grows_with_words_per_page()
    {
        Assert.Equal(3.4, KineticMath.StageFactor(3), 5);
        Assert.True(KineticMath.StageFactor(5) > KineticMath.StageFactor(1));
    }

    [Fact]
    public void Size_variety_zero_gives_uniform_sizes()
    {
        Assert.Equal(1.0, KineticMath.SizeFactor(0.0, 0));
        Assert.Equal(1.0, KineticMath.SizeFactor(0.99, 0));
    }

    [Fact]
    public void Size_variety_one_spans_the_original_range()
    {
        Assert.Equal(0.6, KineticMath.SizeFactor(0.0, 1), 5);
        Assert.Equal(1.25, KineticMath.SizeFactor(1.0, 1), 5);
    }

    [Fact]
    public void Unit_is_stable_and_in_range()
    {
        for (var i = 0; i < 200; i++)
        {
            var value = KineticMath.Unit("ballin", i, 1);
            Assert.InRange(value, 0.0, 0.9999);
            Assert.Equal(value, KineticMath.Unit("ballin", i, 1));
        }
    }

    [Fact]
    public void Unit_differs_by_salt_and_word()
    {
        Assert.NotEqual(KineticMath.Unit("on", 1, 1), KineticMath.Unit("on", 1, 2));
        Assert.NotEqual(KineticMath.Unit("on", 1, 1), KineticMath.Unit("my", 1, 1));
    }

    [Fact]
    public void Pop_starts_at_half_size_and_settles_at_full_size()
    {
        Assert.Equal(0.5, KineticMath.PopScale(0), 3);
        Assert.Equal(0.5, KineticMath.PopScale(-1), 3);
        Assert.Equal(1.0, KineticMath.PopScale(KineticMath.PopSeconds));
        Assert.Equal(1.0, KineticMath.PopScale(5));
    }

    [Fact]
    public void Pop_overshoots_before_settling()
    {
        var peak = Enumerable.Range(1, 19).Max(i => KineticMath.PopScale(i * KineticMath.PopSeconds / 20));

        Assert.True(peak > 1.0);
    }

    [Fact]
    public void Pop_fades_in_quickly()
    {
        Assert.Equal(0, KineticMath.PopOpacity(0));
        Assert.Equal(1, KineticMath.PopOpacity(0.5));
    }
}
