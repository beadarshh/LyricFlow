using System.Diagnostics;
using System.Text.Json;

namespace LyricFlow.Settings;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _path;

    private SettingsStore(string path, AppSettings current)
    {
        _path = path;
        Current = current;
    }

    public AppSettings Current { get; }

    /// <summary>Raised after <see cref="Update"/>; the settings are applied live.</summary>
    public event Action? Changed;

    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LyricFlow", "settings.json");

    public static SettingsStore Load(string path)
    {
        var settings = new AppSettings();
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                settings = JsonSerializer.Deserialize<AppSettings>(json) ?? settings;
                MigrateBackground(json, settings);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Settings load failed, using defaults: {ex.Message}");
        }

        return new SettingsStore(path, settings);
    }

    /// <summary>
    /// An earlier version had only a background colour (transparent by default). If a file has a
    /// visible colour and no background mode, keep that colour as a custom background.
    /// </summary>
    private static void MigrateBackground(string json, AppSettings settings)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || root.TryGetProperty(nameof(AppSettings.Background), out _)
            || !root.TryGetProperty(nameof(AppSettings.BackgroundColor), out _))
        {
            return;
        }

        if (!IsFullyTransparent(settings.BackgroundColor))
            settings.Background = BackgroundMode.Custom;
    }

    private static bool IsFullyTransparent(string color) =>
        color.Length == 9 && color[0] == '#' && color.AsSpan(1, 2).SequenceEqual("00");

    public void Update(Action<AppSettings> mutate)
    {
        mutate(Current);
        Changed?.Invoke();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(Current, JsonOptions));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Settings save failed: {ex.Message}");
        }
    }
}
