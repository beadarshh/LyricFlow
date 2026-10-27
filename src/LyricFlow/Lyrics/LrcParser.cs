using System.Globalization;
using System.Text.RegularExpressions;

namespace LyricFlow.Lyrics;

public static class LrcParser
{
    /// <summary>Longest a line is assumed to be sung for; keeps word fills from crawling across long gaps.</summary>
    public static readonly TimeSpan MaxLineDuration = TimeSpan.FromSeconds(8);

    public static readonly TimeSpan LastLineDuration = TimeSpan.FromSeconds(6);

    private static readonly Regex Timestamp = new(@"\G\[(\d{1,3}):(\d{1,2})(?:[.:](\d{1,3}))?\]", RegexOptions.Compiled);
    private static readonly Regex OffsetTag = new(@"^\[offset:\s*([+-]?\d+)\s*\]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex InlineWordTag = new(@"<\d+:\d+(?:[.:]\d+)?>", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    public static IReadOnlyList<LyricLine> Parse(string lrc)
    {
        var entries = new List<(TimeSpan Start, string Text)>();
        var offset = TimeSpan.Zero;

        foreach (var rawLine in lrc.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            var offsetMatch = OffsetTag.Match(line);
            if (offsetMatch.Success)
            {
                // A positive offset shifts lyrics earlier, per the LRC convention.
                offset = TimeSpan.FromMilliseconds(int.Parse(offsetMatch.Groups[1].Value, CultureInfo.InvariantCulture));
                continue;
            }

            var starts = new List<TimeSpan>();
            var pos = 0;
            Match match;
            while ((match = Timestamp.Match(line, pos)).Success)
            {
                starts.Add(ToTimeSpan(match));
                pos = match.Index + match.Length;
            }

            if (starts.Count == 0)
                continue;

            var text = Whitespace.Replace(InlineWordTag.Replace(line[pos..], string.Empty), " ").Trim();
            foreach (var start in starts)
                entries.Add((start, text));
        }

        // OrderBy is stable, so lines sharing a timestamp keep their file order.
        var ordered = entries
            .Select(e => (Start: Max(e.Start - offset, TimeSpan.Zero), e.Text))
            .OrderBy(e => e.Start)
            .ToList();

        var lines = new List<LyricLine>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var (start, text) = ordered[i];
            var sungEnd = i + 1 < ordered.Count
                ? Min(ordered[i + 1].Start, start + MaxLineDuration)
                : start + LastLineDuration;

            var words = text.Length == 0 ? Array.Empty<LyricWord>() : WordTimer.Split(text, start, sungEnd);
            lines.Add(new LyricLine(start, sungEnd, text, words));
        }

        return lines;
    }

    private static TimeSpan ToTimeSpan(Match match)
    {
        var minutes = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var seconds = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var fraction = 0.0;
        if (match.Groups[3].Success)
        {
            var digits = match.Groups[3].Value;
            fraction = int.Parse(digits, CultureInfo.InvariantCulture) / Math.Pow(10, digits.Length);
        }

        return TimeSpan.FromSeconds(minutes * 60 + seconds + fraction);
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
}
