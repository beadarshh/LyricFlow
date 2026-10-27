using System.Text.RegularExpressions;

namespace LyricFlow.Lyrics;

/// <summary>Strips the decoration players and video sites add to titles and artists.</summary>
public static class TrackNormalizer
{
    private static readonly Regex Noise = new(
        @"\s*[\(\[][^\)\]]*\b(official|video|audio|lyrics?|visuali[sz]er|hd|4k|mv|remaster(ed)?)\b[^\)\]]*[\)\]]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Featuring = new(
        @"\s*[\(\[](feat|ft)\.?\s[^\)\]]*[\)\]]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RemasterSuffix = new(
        @"\s+-\s+(\d{4}\s+)?(remaster(ed)?|mono|stereo|single version|radio edit)\b.*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string CleanTitle(string title)
    {
        var cleaned = Noise.Replace(title, string.Empty);
        cleaned = Featuring.Replace(cleaned, string.Empty);
        cleaned = RemasterSuffix.Replace(cleaned, string.Empty);
        return cleaned.Trim();
    }

    public static string CleanArtist(string artist)
    {
        var cleaned = artist;
        if (cleaned.EndsWith(" - Topic", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[..^" - Topic".Length];

        var separator = cleaned.IndexOf(';');
        if (separator >= 0)
            cleaned = cleaned[..separator];

        return cleaned.Trim();
    }
}
