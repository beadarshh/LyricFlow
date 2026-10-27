using LyricFlow.Settings;

namespace LyricFlow.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "LyricFlowTests-" + Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        var store = SettingsStore.Load(SettingsPath);

        Assert.Equal(new AppSettings().FontFamily, store.Current.FontFamily);
        Assert.Null(store.Current.Left);
    }

    [Fact]
    public void Saved_settings_survive_a_reload()
    {
        var store = SettingsStore.Load(SettingsPath);
        store.Update(s =>
        {
            s.FontSize = 52;
            s.Left = 120.5;
            s.MiniMode = true;
            s.OffsetMs = -300;
        });
        store.Save();

        var reloaded = SettingsStore.Load(SettingsPath).Current;

        Assert.Equal(52, reloaded.FontSize);
        Assert.Equal(120.5, reloaded.Left);
        Assert.True(reloaded.MiniMode);
        Assert.Equal(-300, reloaded.OffsetMs);
    }

    [Fact]
    public void Style_defaults_to_kinetic_and_is_stored_by_name()
    {
        var store = SettingsStore.Load(SettingsPath);
        Assert.Equal(OverlayStyle.Kinetic, store.Current.Style);

        store.Update(s => s.Style = OverlayStyle.Classic);
        store.Save();

        Assert.Contains("\"Classic\"", File.ReadAllText(SettingsPath));
        Assert.Equal(OverlayStyle.Classic, SettingsStore.Load(SettingsPath).Current.Style);
    }

    [Fact]
    public void New_options_have_sensible_defaults_and_survive_a_reload()
    {
        var defaults = SettingsStore.Load(SettingsPath).Current;
        Assert.Equal(BackgroundMode.Transparent, defaults.Background);
        Assert.True(defaults.CenterOnHotkey);
        Assert.Equal(3, defaults.KineticWordsPerPage);
        Assert.True(defaults.KineticPop);

        var store = SettingsStore.Load(SettingsPath);
        store.Update(s =>
        {
            s.BackgroundColor = "#CC101820";
            s.OverlayWidth = 1200;
            s.KineticWordsPerPage = 2;
            s.KineticTilt = 7.5;
            s.KineticPop = false;
            s.CenterOnHotkey = false;
        });
        store.Save();

        var reloaded = SettingsStore.Load(SettingsPath).Current;
        Assert.Equal("#CC101820", reloaded.BackgroundColor);
        Assert.Equal(1200, reloaded.OverlayWidth);
        Assert.Equal(2, reloaded.KineticWordsPerPage);
        Assert.Equal(7.5, reloaded.KineticTilt);
        Assert.False(reloaded.KineticPop);
        Assert.False(reloaded.CenterOnHotkey);
    }

    [Theory]
    [InlineData(BackgroundMode.Transparent, "#00000000")]
    [InlineData(BackgroundMode.Black, "#FF000000")]
    [InlineData(BackgroundMode.Custom, "#336699")]
    public void Effective_background_follows_the_mode(BackgroundMode mode, string expected)
    {
        var settings = new AppSettings { Background = mode, BackgroundColor = "#336699" };

        Assert.Equal(expected, settings.EffectiveBackgroundColor);
    }

    [Fact]
    public void Background_mode_is_stored_by_name()
    {
        var store = SettingsStore.Load(SettingsPath);
        store.Update(s => s.Background = BackgroundMode.Black);
        store.Save();

        Assert.Contains("\"Black\"", File.ReadAllText(SettingsPath));
        Assert.Equal(BackgroundMode.Black, SettingsStore.Load(SettingsPath).Current.Background);
    }

    [Theory]
    [InlineData("#CC101820", BackgroundMode.Custom)]
    [InlineData("#336699", BackgroundMode.Custom)]
    [InlineData("#00000000", BackgroundMode.Transparent)]
    public void A_colour_from_before_background_modes_is_kept_as_custom(string color, BackgroundMode expected)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, $"{{ \"BackgroundColor\": \"{color}\" }}");

        var s = SettingsStore.Load(SettingsPath).Current;

        Assert.Equal(expected, s.Background);
        Assert.Equal(color, s.BackgroundColor);
    }

    [Fact]
    public void An_explicit_mode_is_never_overridden_by_migration()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, "{ \"Background\": \"Transparent\", \"BackgroundColor\": \"#CC101820\" }");

        Assert.Equal(BackgroundMode.Transparent, SettingsStore.Load(SettingsPath).Current.Background);
    }

    [Fact]
    public void Old_settings_file_without_new_fields_still_loads()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, "{ \"FontFamily\": \"Arial\", \"FontSize\": 40, \"OffsetMs\": 950 }");

        var s = SettingsStore.Load(SettingsPath).Current;

        Assert.Equal("Arial", s.FontFamily);
        Assert.Equal(950, s.OffsetMs);
        Assert.Equal(OverlayStyle.Kinetic, s.Style);
        Assert.Equal(900, s.OverlayWidth);
    }

    [Fact]
    public void Update_raises_changed()
    {
        var store = SettingsStore.Load(SettingsPath);
        var raised = 0;
        store.Changed += () => raised++;

        store.Update(s => s.Opacity = 0.5);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Corrupt_file_falls_back_to_defaults()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, "{ nope");

        var store = SettingsStore.Load(SettingsPath);

        Assert.Equal(new AppSettings().FontSize, store.Current.FontSize);
    }
}
