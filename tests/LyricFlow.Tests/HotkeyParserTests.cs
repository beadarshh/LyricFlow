using LyricFlow.UI;

namespace LyricFlow.Tests;

public class HotkeyParserTests
{
    [Fact]
    public void Parses_modifiers_and_key()
    {
        Assert.True(HotkeyParser.TryParse("Ctrl+Alt+L", out var modifiers, out var key));
        Assert.Equal(HotkeyParser.Control | HotkeyParser.Alt, modifiers);
        Assert.Equal((uint)'L', key);
    }

    [Fact]
    public void Parses_function_keys_case_insensitively()
    {
        Assert.True(HotkeyParser.TryParse("shift+f9", out var modifiers, out var key));
        Assert.Equal(HotkeyParser.Shift, modifiers);
        Assert.Equal(0x78u, key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("L")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Alt+L+K")]
    [InlineData("Ctrl+Banana")]
    [InlineData("Ctrl+F13")]
    public void Rejects_invalid_hotkeys(string text)
    {
        Assert.False(HotkeyParser.TryParse(text, out _, out _));
    }

    [Theory]
    [InlineData("Ctrl+Alt+L")]
    [InlineData("Ctrl+Shift+F5")]
    [InlineData("Alt+Win+9")]
    public void Format_round_trips(string text)
    {
        Assert.True(HotkeyParser.TryParse(text, out var modifiers, out var key));
        Assert.Equal(text, HotkeyParser.Format(modifiers, key));
    }
}
