using LyricFlow.Lyrics;

namespace LyricFlow.UI;

/// <summary>The pure parts of the Kinetic style: paging, deterministic variation and the pop-in curve.</summary>
public static class KineticMath
{
    public const int MinWordsPerPage = 1;
    public const int MaxWordsPerPage = 5;
    public const double PopSeconds = 0.2;
    private const double FadeInSeconds = 0.08;

    /// <summary>Index of the last word that has started at <paramref name="position"/>, or -1 before the first.</summary>
    public static int ActiveWordIndex(IReadOnlyList<LyricWord> words, TimeSpan position)
    {
        int lo = 0, hi = words.Count - 1, result = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (words[mid].Start <= position)
            {
                result = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return result;
    }

    /// <summary>Which page a word is on, its row on that page, and how many rows the page has.</summary>
    public static (int Page, int Slot, int PageSize) Locate(int wordIndex, int wordCount, int wordsPerPage)
    {
        wordsPerPage = Math.Clamp(wordsPerPage, MinWordsPerPage, MaxWordsPerPage);
        var page = wordIndex / wordsPerPage;
        var first = page * wordsPerPage;
        return (page, wordIndex - first, Math.Min(wordsPerPage, wordCount - first));
    }

    /// <summary>Height of the area the stacked words live in, in multiples of the base word size.</summary>
    public static double StageFactor(int wordsPerPage) =>
        1.1 * Math.Clamp(wordsPerPage, MinWordsPerPage, MaxWordsPerPage) + 0.1;

    /// <summary>Size multiplier for a word: 1 when <paramref name="variety"/> is 0, else between 0.6 and 1.25 of the base.</summary>
    public static double SizeFactor(double unit, double variety) =>
        1 + Math.Clamp(variety, 0, 1) * (0.65 * unit - 0.4);

    /// <summary>A stable value in [0, 1) for a word, so its size, tilt and position don't change between redraws.</summary>
    public static double Unit(string text, int index, int salt)
    {
        var hash = 2166136261u;
        foreach (var c in text)
            hash = (hash ^ c) * 16777619u;
        hash = (hash ^ (uint)index) * 16777619u;
        hash = (hash ^ (uint)salt) * 16777619u;
        hash ^= hash >> 15;
        return hash % 10000 / 10000.0;
    }

    /// <summary>Scale of a word <paramref name="ageSeconds"/> after it starts: grows from half size with a small overshoot.</summary>
    public static double PopScale(double ageSeconds)
    {
        if (ageSeconds >= PopSeconds)
            return 1;

        const double c1 = 1.70158;
        const double c3 = c1 + 1;
        var t = Math.Max(ageSeconds, 0) / PopSeconds - 1;
        var easeOutBack = 1 + c3 * t * t * t + c1 * t * t;
        return 0.5 + 0.5 * easeOutBack;
    }

    public static double PopOpacity(double ageSeconds) => Math.Clamp(ageSeconds / FadeInSeconds, 0, 1);
}
