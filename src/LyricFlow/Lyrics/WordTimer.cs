using System.Text.RegularExpressions;

namespace LyricFlow.Lyrics;

/// <summary>Estimates per-word timing by splitting a line's duration across its words by length.</summary>
public static class WordTimer
{
    private const int MinWeight = 3;

    // Kana and Han characters are not space-separated, so each one counts as a word.
    private static readonly Regex Tokens = new(@"[぀-ヿ㐀-鿿]|[^\s぀-ヿ㐀-鿿]+", RegexOptions.Compiled);

    public static IReadOnlyList<LyricWord> Split(string text, TimeSpan start, TimeSpan end)
    {
        if (end < start)
            end = start;

        var matches = Tokens.Matches(text);
        if (matches.Count == 0)
            return Array.Empty<LyricWord>();

        var weights = new double[matches.Count];
        var total = 0.0;
        for (var i = 0; i < matches.Count; i++)
        {
            var letters = matches[i].Value.Count(char.IsLetterOrDigit);
            weights[i] = Math.Max(letters, MinWeight);
            total += weights[i];
        }

        var words = new List<LyricWord>(matches.Count);
        var span = (end - start).Ticks;
        var cursor = 0.0;
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var wordStart = start + TimeSpan.FromTicks((long)(span * cursor / total));
            cursor += weights[i];
            var wordEnd = i == matches.Count - 1
                ? end
                : start + TimeSpan.FromTicks((long)(span * cursor / total));

            var followedBySpace = match.Index + match.Length < text.Length && char.IsWhiteSpace(text[match.Index + match.Length]);
            words.Add(new LyricWord(followedBySpace ? match.Value + " " : match.Value, wordStart, wordEnd));
        }

        return words;
    }
}
