using System.Text.Json.Serialization;

namespace LyricFlow.Settings;

public enum OverlayStyle
{
    /// <summary>The current line with a per-word fill, next line dimmed below.</summary>
    Classic,

    /// <summary>One big word at a time, stacked, with the sung word on a colour block.</summary>
    Kinetic,
}

public enum BackgroundMode
{
    /// <summary>Lyrics float over whatever is on screen.</summary>
    Transparent,

    /// <summary>A solid black backdrop.</summary>
    Black,

    /// <summary>A backdrop in the chosen colour.</summary>
    Custom,
}

public sealed class AppSettings
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OverlayStyle Style { get; set; } = OverlayStyle.Kinetic;

    public string FontFamily { get; set; } = "Segoe UI";

    public double FontSize { get; set; } = 36;

    /// <summary>Colour of words not yet sung (and of the next line). Classic style.</summary>
    public string BaseColor { get; set; } = "#99FFFFFF";

    /// <summary>Colour of words as they are sung. Classic style.</summary>
    public string HighlightColor { get; set; } = "#FFFFFFFF";

    /// <summary>Colour block behind the word being sung. Kinetic style.</summary>
    public string KineticBlockColor { get; set; } = "#E8365D";

    /// <summary>Colour of words already sung on the current page. Kinetic style.</summary>
    public string KineticWordColor { get; set; } = "#E8372B";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BackgroundMode Background { get; set; } = BackgroundMode.Transparent;

    /// <summary>The backdrop colour used when <see cref="Background"/> is Custom: #RRGGBB or #AARRGGBB.</summary>
    public string BackgroundColor { get; set; } = "#1B1F2A";

    /// <summary>With a Black or Custom background, fill the whole screen instead of drawing a box.</summary>
    public bool CoverScreen { get; set; } = true;

    /// <summary>The backdrop colour actually drawn, as #AARRGGBB.</summary>
    [JsonIgnore]
    public string EffectiveBackgroundColor => Background switch
    {
        BackgroundMode.Black => "#FF000000",
        BackgroundMode.Custom => BackgroundColor,
        _ => "#00000000",
    };

    /// <summary>Width of the overlay window in DIPs.</summary>
    public double OverlayWidth { get; set; } = 900;

    /// <summary>Kinetic style: how many words are stacked before the page clears (1-5).</summary>
    public int KineticWordsPerPage { get; set; } = 3;

    /// <summary>Kinetic style: maximum tilt of a word, in degrees either way.</summary>
    public double KineticTilt { get; set; } = 4;

    /// <summary>Kinetic style: how much word sizes differ, from 0 (all alike) to 1.</summary>
    public double KineticSizeVariety { get; set; } = 1.0;

    /// <summary>Kinetic style: words grow in with a small bounce.</summary>
    public bool KineticPop { get; set; } = true;

    /// <summary>The hotkey opens the overlay in the middle of the screen the mouse is on.</summary>
    public bool CenterOnHotkey { get; set; } = true;

    public double Opacity { get; set; } = 1.0;

    /// <summary>Window position in DIPs; null means bottom-centre of the primary screen.</summary>
    public double? Left { get; set; }

    public double? Top { get; set; }

    /// <summary>Show only the current line. Classic style.</summary>
    public bool MiniMode { get; set; }

    public bool Visible { get; set; } = true;

    public string Hotkey { get; set; } = "Ctrl+Alt+L";

    /// <summary>Added to the playback position: positive shows lyrics earlier, negative later.</summary>
    public int OffsetMs { get; set; }
}
