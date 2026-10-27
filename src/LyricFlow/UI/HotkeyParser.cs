namespace LyricFlow.UI;

/// <summary>Converts between "Ctrl+Alt+L" and the modifier/virtual-key pair RegisterHotKey wants.</summary>
public static class HotkeyParser
{
    public const uint Alt = 0x1;
    public const uint Control = 0x2;
    public const uint Shift = 0x4;
    public const uint Win = 0x8;

    public static bool TryParse(string? text, out uint modifiers, out uint virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= Control;
                    break;
                case "alt":
                    modifiers |= Alt;
                    break;
                case "shift":
                    modifiers |= Shift;
                    break;
                case "win":
                    modifiers |= Win;
                    break;
                default:
                    if (virtualKey != 0 || !TryParseKey(part, out virtualKey))
                        return false;
                    break;
            }
        }

        // A bare key would swallow normal typing system-wide.
        return virtualKey != 0 && modifiers != 0;
    }

    public static string Format(uint modifiers, uint virtualKey)
    {
        var parts = new List<string>();
        if ((modifiers & Control) != 0) parts.Add("Ctrl");
        if ((modifiers & Alt) != 0) parts.Add("Alt");
        if ((modifiers & Shift) != 0) parts.Add("Shift");
        if ((modifiers & Win) != 0) parts.Add("Win");
        parts.Add(KeyName(virtualKey));
        return string.Join("+", parts);
    }

    public static bool IsSupportedKey(uint virtualKey) =>
        virtualKey is >= 0x41 and <= 0x5A or >= 0x30 and <= 0x39 or >= 0x70 and <= 0x7B;

    private static bool TryParseKey(string name, out uint virtualKey)
    {
        virtualKey = 0;
        var upper = name.ToUpperInvariant();

        if (upper.Length == 1 && (char.IsAsciiLetterUpper(upper[0]) || char.IsAsciiDigit(upper[0])))
        {
            virtualKey = upper[0];
            return true;
        }

        if (upper.Length is 2 or 3 && upper[0] == 'F' && int.TryParse(upper[1..], out var n) && n is >= 1 and <= 12)
        {
            virtualKey = (uint)(0x70 + n - 1);
            return true;
        }

        return false;
    }

    private static string KeyName(uint virtualKey) =>
        virtualKey is >= 0x70 and <= 0x7B ? "F" + (virtualKey - 0x70 + 1) : ((char)virtualKey).ToString();
}
