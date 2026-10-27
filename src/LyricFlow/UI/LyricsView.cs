using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using LyricFlow.Lyrics;
using LyricFlow.Media;
using LyricFlow.Settings;

namespace LyricFlow.UI;

/// <summary>
/// Draws the current lyric line with a per-word fill, the next line dimmed below it, and the
/// fades between them. Redraws every frame only while something is animating.
/// </summary>
public sealed class LyricsView : FrameworkElement
{
    private const int Unset = int.MinValue;
    private const double TransitionSeconds = 0.35;
    private const double HintSeconds = 4.0;
    private const double MinDrawIntervalSeconds = 0.030;
    private const double NextScale = 0.7;
    private const double SidePadding = 24;
    private const double RowHeightFactor = 1.3;

    private const double BackgroundCornerRadius = 18;

    // Kinetic style: words are drawn at 2.5x the font size; see KineticMath.StageFactor for the stage height.
    private const double KineticScale = 2.5;
    private const double KineticIdleSeconds = 1.0;
    private const double KineticIdleFadeSeconds = 0.5;

    private enum Fill { None, Progress, Full }

    private sealed record ViewStyle(
        Typeface Typeface,
        double Size,
        Brush Base,
        Brush Highlight,
        Brush Shadow,
        double Opacity,
        bool Mini,
        bool Kinetic,
        Brush Block,
        Brush Word,
        Brush Background,
        int WordsPerPage,
        double Tilt,
        double SizeVariety,
        bool Pop);

    private sealed class KineticWord
    {
        public required LyricWord Word { get; init; }
        public required FormattedText Active { get; init; }
        public required FormattedText Past { get; init; }
        public required FormattedText Shadow { get; init; }
        public required double Size { get; init; }
        public required double Angle { get; init; }
        public required double OffsetX { get; init; }
        public required double CenterY { get; init; }
        public double Width => Past.Width;
        public double Height => Past.Height;
    }

    private sealed class WordSlot
    {
        public required LyricWord Word { get; init; }
        public double X { get; set; }
        public required double Width { get; init; }
        public required FormattedText Shadow { get; init; }
        public required FormattedText Dim { get; init; }
        public required FormattedText Bright { get; init; }
    }

    private sealed class Row
    {
        public double Y { get; init; }
        public List<WordSlot> Slots { get; } = new();
    }

    private sealed class LineLayout
    {
        public List<Row> Rows { get; } = new();
        public double Height { get; set; }
    }

    private readonly Stopwatch _clockWatch = Stopwatch.StartNew();
    private readonly Dictionary<(int Index, bool Next), LineLayout> _layouts = new();
    private readonly Dictionary<string, LineLayout> _textLayouts = new();
    private readonly Dictionary<int, KineticWord[]> _kineticLayouts = new();

    private PlaybackClock? _clock;
    private SettingsStore? _store;
    private ViewStyle? _style;
    private LyricsResult? _lyrics;
    private string? _message;
    private bool _hasSession;
    private bool _isPlaying;
    private bool _moveMode;
    private bool _fullscreen;
    private bool _dirty = true;
    private bool _subscribed;
    private double _lastFrame;
    private double _lastDraw;
    private int _kineticActive = -1;
    private double _hintStartedAt;
    private double _alpha;
    private int _index = Unset;
    private int _prevIndex = Unset;
    private double _transition = 1;

    public LyricsView()
    {
        IsHitTestVisible = false;
        IsVisibleChanged += (_, _) => UpdateSubscription();
        Loaded += (_, _) => UpdateSubscription();
        Unloaded += (_, _) => UpdateSubscription();
        SizeChanged += (_, _) => InvalidateLayouts();
    }

    public void Bind(PlaybackClock clock, SettingsStore store)
    {
        _clock = clock;
        _store = store;
        store.Changed += OnSettingsChanged;
        RebuildStyle();
    }

    public void SetSession(bool hasSession)
    {
        _hasSession = hasSession;
        _dirty = true;
    }

    public void SetPlaying(bool isPlaying)
    {
        _isPlaying = isPlaying;
        _dirty = true;
    }

    /// <summary>Null means "loading": nothing is drawn until the lookup finishes.</summary>
    public void SetLyrics(LyricsResult? lyrics)
    {
        _lyrics = lyrics;
        _index = Unset;
        _prevIndex = Unset;
        _transition = 1;
        _layouts.Clear();
        _kineticLayouts.Clear();
        _hintStartedAt = _clockWatch.Elapsed.TotalSeconds;
        _message = lyrics?.Kind switch
        {
            LyricsKind.Plain => "Synced lyrics unavailable",
            LyricsKind.NotFound => "No lyrics found",
            LyricsKind.Error => "Couldn't load lyrics",
            LyricsKind.Instrumental => "♪",
            _ => null,
        };
        _dirty = true;
    }

    /// <summary>A full-screen backdrop has square corners and the lyrics centred in it.</summary>
    public void SetFullscreen(bool on)
    {
        _fullscreen = on;
        _dirty = true;
    }

    public void SetMoveMode(bool on)
    {
        _moveMode = on;
        _dirty = true;
    }

    private void OnSettingsChanged()
    {
        RebuildStyle();
        InvalidateLayouts();
        InvalidateMeasure();
    }

    private void RebuildStyle()
    {
        var s = _store!.Current;
        _style = new ViewStyle(
            new Typeface(new FontFamily(s.FontFamily), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            Math.Clamp(s.FontSize, 12, 120),
            FrozenBrush(s.BaseColor, Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
            FrozenBrush(s.HighlightColor, Colors.White),
            FrozenBrush("#B0000000", Colors.Black),
            Math.Clamp(s.Opacity, 0.1, 1.0),
            s.MiniMode,
            s.Style == OverlayStyle.Kinetic,
            FrozenBrush(s.KineticBlockColor, Color.FromRgb(0xE8, 0x36, 0x5D)),
            FrozenBrush(s.KineticWordColor, Color.FromRgb(0xE8, 0x37, 0x2B)),
            FrozenBrush(s.EffectiveBackgroundColor, Colors.Transparent),
            Math.Clamp(s.KineticWordsPerPage, KineticMath.MinWordsPerPage, KineticMath.MaxWordsPerPage),
            Math.Clamp(s.KineticTilt, 0, 15),
            Math.Clamp(s.KineticSizeVariety, 0, 1),
            s.KineticPop);
    }

    /// <summary>Called when the overlay is shown on demand, so the "nothing playing" hint appears again.</summary>
    public void NotifyShown()
    {
        _hintStartedAt = _clockWatch.Elapsed.TotalSeconds;
        _dirty = true;
    }

    private static Brush FrozenBrush(string text, Color fallback)
    {
        Color color;
        try
        {
            color = (Color)ColorConverter.ConvertFromString(text);
        }
        catch (FormatException)
        {
            color = fallback;
        }

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void InvalidateLayouts()
    {
        _layouts.Clear();
        _textLayouts.Clear();
        _kineticLayouts.Clear();
        _dirty = true;
    }

    private void UpdateSubscription()
    {
        var shouldRun = IsLoaded && IsVisible;
        if (shouldRun == _subscribed)
            return;

        _subscribed = shouldRun;
        if (shouldRun)
        {
            _lastFrame = _clockWatch.Elapsed.TotalSeconds;
            _dirty = true;
            CompositionTarget.Rendering += OnRendering;
        }
        else
        {
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    protected override Size MeasureOverride(Size available)
    {
        if (_style is null)
            return new Size(0, 0);

        var width = double.IsInfinity(available.Width) ? 800 : available.Width;
        return new Size(width, DesiredHeight(_style));
    }

    /// <summary>Height of the lyrics themselves; any extra room (a full-screen window) is left around them.</summary>
    private static double DesiredHeight(ViewStyle style)
    {
        if (style.Kinetic)
            return KineticStageHeight(style) + 8;

        var rowHeight = style.Size * RowHeightFactor;
        var height = rowHeight * 2 + 8;
        if (!style.Mini)
            height += rowHeight * NextScale * 2 + 4;

        return height;
    }

    private static double KineticStageHeight(ViewStyle style) =>
        style.Size * KineticScale * KineticMath.StageFactor(style.WordsPerPage);

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = _clockWatch.Elapsed.TotalSeconds;
        var dt = Math.Min(now - _lastFrame, 0.1);
        _lastFrame = now;

        var dirty = _dirty;
        _dirty = false;

        var target = TargetAlpha(now);
        if (Math.Abs(_alpha - target) > 0.003)
        {
            _alpha += (target - _alpha) * (1 - Math.Exp(-dt * 8));
            dirty = true;
        }
        else if (_alpha != target)
        {
            _alpha = target;
            dirty = true;
        }

        if (_lyrics is { Kind: LyricsKind.Synced } lyrics && _clock is not null)
        {
            var index = lyrics.IndexAt(Position());
            if (index != _index)
            {
                _prevIndex = _index;
                _index = index;
                _transition = 0;
                dirty = true;
            }

            // Classic's word fill advances continuously; Kinetic only changes at word boundaries.
            if (_isPlaying && _alpha > 0.003 && (_style is { Kinetic: false } || KineticNeedsFrame(lyrics)))
                dirty = true;
        }

        if (_transition < 1)
        {
            _transition = Math.Min(1, _transition + dt / TransitionSeconds);
            dirty = true;
        }

        if (!dirty)
            return;

        // 30 fps is plenty for lyrics and halves the redraw work on a 60 Hz display.
        if (now - _lastDraw < MinDrawIntervalSeconds)
        {
            _dirty = true;
            return;
        }

        _lastDraw = now;
        InvalidateVisual();
    }

    /// <summary>True when the kinetic page changed or is mid-animation (pop-in, or fading out after the line ended).</summary>
    private bool KineticNeedsFrame(LyricsResult lyrics)
    {
        if (_index < 0 || _index >= lyrics.Lines.Count)
            return false;

        var position = Position();
        var line = lyrics.Lines[_index];
        var active = KineticMath.ActiveWordIndex(line.Words, position);

        var changed = active != _kineticActive;
        _kineticActive = active;
        if (changed)
            return true;
        if (active < 0)
            return false;

        var age = (position - line.Words[active].Start).TotalSeconds;
        if (_style!.Pop && age < KineticMath.PopSeconds + 0.05)
            return true;

        var sinceIdle = (position - line.End).TotalSeconds - KineticIdleSeconds;
        return sinceIdle >= 0 && sinceIdle <= KineticIdleFadeSeconds + 0.05;
    }

    private TimeSpan Position() =>
        _clock!.Now + TimeSpan.FromMilliseconds(_store!.Current.OffsetMs);

    private double TargetAlpha(double now)
    {
        if (_moveMode)
            return 1;
        if (!_hasSession)
            return now - _hintStartedAt < HintSeconds ? 0.6 : 0;
        if (_lyrics is null)
            return 0;

        switch (_lyrics.Kind)
        {
            case LyricsKind.Synced:
                return _isPlaying ? 1 : 0.4;
            case LyricsKind.Instrumental:
                return _isPlaying ? 0.7 : 0.3;
            default:
                return now - _hintStartedAt < HintSeconds ? 0.9 : 0;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_style is null)
            return;

        // The backdrop is drawn whenever the window is up, whatever the lyrics are doing.
        var radius = _fullscreen ? 0 : BackgroundCornerRadius;
        dc.DrawRoundedRectangle(_style.Background, null, new Rect(0, 0, ActualWidth, ActualHeight), radius, radius);

        if (_alpha < 0.004)
            return;

        var rowHeight = _style.Size * RowHeightFactor;

        // Messages sit in the first slot of the classic layout, or mid-stage in the kinetic one.
        var slotHeight = _style.Kinetic ? KineticStageHeight(_style) : rowHeight * 2;

        // In a full-screen window the lyrics sit in the middle; otherwise the window is exactly as tall as they are.
        dc.PushTransform(new TranslateTransform(0, Math.Max(0, (ActualHeight - DesiredHeight(_style)) / 2)));
        dc.PushOpacity(Math.Min(1, _alpha * _style.Opacity));
        if (_moveMode)
            DrawText(dc, "Drag to move · lock from the tray menu", slotHeight, 0, 1, highlight: true);
        else if (_lyrics is { Kind: LyricsKind.Synced } lyrics)
        {
            if (_style.Kinetic)
                DrawKinetic(dc, lyrics, Position());
            else
                DrawSynced(dc, lyrics, rowHeight, slotHeight);
        }
        else if (!_hasSession)
            DrawText(dc, "Play a song to see lyrics", slotHeight, 0, 1, highlight: false);
        else if (_message is not null)
            DrawText(dc, _message, slotHeight, 0, 1, highlight: false);
        dc.Pop();
        dc.Pop();
    }

    /// <summary>
    /// Kinetic style: the line's words appear one at a time, stacked in pages. The word
    /// being sung is white on a colour block; earlier words on the page stay in the accent colour.
    /// </summary>
    private void DrawKinetic(DrawingContext dc, LyricsResult lyrics, TimeSpan position)
    {
        if (_index < 0 || _index >= lyrics.Lines.Count)
            return;

        var line = lyrics.Lines[_index];
        if (line.IsGap || line.Words.Count == 0)
            return;

        var active = KineticMath.ActiveWordIndex(line.Words, position);
        if (active < 0)
            return;

        var words = GetKineticWords(_index, line);
        var perPage = _style!.WordsPerPage;
        var firstOnPage = KineticMath.Locate(active, words.Length, perPage).Page * perPage;

        // Once the line has been sung and the next one is slow to arrive, let the page fade out.
        var sinceLineEnd = (position - line.End).TotalSeconds - KineticIdleSeconds;
        var idleOpacity = Math.Clamp(1 - sinceLineEnd / KineticIdleFadeSeconds, 0, 1);

        dc.PushOpacity(idleOpacity);
        for (var i = firstOnPage; i <= active; i++)
            DrawKineticWord(dc, words[i], i == active, position);
        dc.Pop();
    }

    private void DrawKineticWord(DrawingContext dc, KineticWord word, bool isActive, TimeSpan position)
    {
        var age = (position - word.Word.Start).TotalSeconds;
        var animate = isActive && _style!.Pop;
        var scale = animate ? KineticMath.PopScale(age) : 1.0;

        dc.PushOpacity(animate ? KineticMath.PopOpacity(age) : 1.0);
        dc.PushTransform(new TranslateTransform(ActualWidth / 2 + word.OffsetX, word.CenterY));
        dc.PushTransform(new RotateTransform(word.Angle));
        dc.PushTransform(new ScaleTransform(scale, scale));

        var origin = new Point(-word.Width / 2, -word.Height / 2);
        if (isActive)
        {
            var padX = word.Size * 0.18;
            var block = new Rect(origin.X - padX, origin.Y + word.Height * 0.1, word.Width + 2 * padX, word.Height * 0.82);
            dc.DrawRoundedRectangle(_style!.Block, null, block, 5, 5);
            dc.DrawText(word.Active, origin);
        }
        else
        {
            dc.DrawText(word.Shadow, new Point(origin.X + 2, origin.Y + 3));
            dc.DrawText(word.Past, origin);
        }

        dc.Pop();
        dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    private KineticWord[] GetKineticWords(int index, LyricLine line)
    {
        if (_kineticLayouts.TryGetValue(index, out var cached))
            return cached;

        var style = _style!;
        var family = new FontFamily(_store!.Current.FontFamily);
        var baseSize = style.Size * KineticScale;
        var stageHeight = KineticStageHeight(style);
        var maxWidth = Math.Max(100, ActualWidth * 0.9);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var culture = CultureInfo.CurrentUICulture;

        var result = new KineticWord[line.Words.Count];
        for (var k = 0; k < result.Length; k++)
        {
            var word = line.Words[k];
            var text = word.Text.Trim();
            var (_, slot, pageSize) = KineticMath.Locate(k, result.Length, style.WordsPerPage);
            var slotHeight = stageHeight / pageSize;

            var typeface = new Typeface(
                family,
                KineticMath.Unit(text, k, 4) < 0.2 ? FontStyles.Italic : FontStyles.Normal,
                FontWeights.Bold,
                FontStretches.Normal);

            FormattedText Make(Brush brush, double size) =>
                new(text, culture, FlowDirection.LeftToRight, typeface, size, brush, dpi);

            var factor = KineticMath.SizeFactor(KineticMath.Unit(text, k, 1), style.SizeVariety);
            var size = Math.Min(baseSize * factor, slotHeight * 0.8);
            var probe = Make(style.Word, size);
            if (probe.Width > maxWidth)
                size *= maxWidth / probe.Width;

            result[k] = new KineticWord
            {
                Word = word,
                Size = size,
                Active = Make(Brushes.White, size),
                Past = Make(style.Word, size),
                Shadow = Make(style.Shadow, size),
                Angle = (KineticMath.Unit(text, k, 2) * 2 - 1) * style.Tilt,
                OffsetX = (KineticMath.Unit(text, k, 3) * 2 - 1) * ActualWidth * 0.1,
                CenterY = slotHeight * (slot + 0.5),
            };
        }

        _kineticLayouts[index] = result;
        return result;
    }

    private void DrawSynced(DrawingContext dc, LyricsResult lyrics, double rowHeight, double slotHeight)
    {
        var position = Position();
        var eased = 1 - Math.Pow(1 - _transition, 3);
        var slide = rowHeight * 0.5;

        if (_transition < 1 && _prevIndex != Unset)
            DrawIndex(dc, lyrics, _prevIndex, slotHeight, -eased * slide, 1 - eased, Fill.Full, position);

        DrawIndex(dc, lyrics, _index, slotHeight, (1 - eased) * slide, eased, Fill.Progress, position);

        // _index is Unset until the first frame after the lyrics arrive; there is no next line yet.
        var nextIndex = _index == Unset ? -1 : _index + 1;
        if (!_style!.Mini && nextIndex >= 0 && nextIndex < lyrics.Lines.Count && !lyrics.Lines[nextIndex].IsGap)
        {
            var next = GetLayout(lyrics, nextIndex, next: true);
            DrawLayout(dc, next, slotHeight + 4, eased * 0.8, Fill.None, position);
        }
    }

    private void DrawIndex(DrawingContext dc, LyricsResult lyrics, int index, double slotHeight, double yOffset, double opacity, Fill fill, TimeSpan position)
    {
        if (index < 0 || index >= lyrics.Lines.Count || lyrics.Lines[index].IsGap)
        {
            // Before the first line, or an instrumental gap.
            DrawText(dc, "♪", slotHeight, yOffset, opacity, highlight: false);
            return;
        }

        var layout = GetLayout(lyrics, index, next: false);
        DrawLayout(dc, layout, (slotHeight - layout.Height) / 2 + yOffset, opacity, fill, position);
    }

    private void DrawText(DrawingContext dc, string text, double slotHeight, double yOffset, double opacity, bool highlight)
    {
        if (!_textLayouts.TryGetValue(text, out var layout))
        {
            layout = BuildLayout(new[] { new LyricWord(text, TimeSpan.Zero, TimeSpan.Zero) }, 1.0);
            _textLayouts[text] = layout;
        }

        DrawLayout(dc, layout, (slotHeight - layout.Height) / 2 + yOffset, opacity, highlight ? Fill.Full : Fill.None, TimeSpan.Zero);
    }

    private void DrawLayout(DrawingContext dc, LineLayout layout, double top, double opacity, Fill fill, TimeSpan position)
    {
        var feather = _style!.Size * 0.45;

        dc.PushOpacity(Math.Clamp(opacity, 0, 1));
        foreach (var row in layout.Rows)
        {
            foreach (var slot in row.Slots)
            {
                var origin = new Point(slot.X, top + row.Y);
                dc.DrawText(slot.Shadow, new Point(origin.X + 1.5, origin.Y + 2));
                dc.DrawText(slot.Dim, origin);

                var progress = fill switch
                {
                    Fill.Full => 1.0,
                    Fill.Progress => WordProgress(slot.Word, position),
                    _ => 0.0,
                };

                if (progress >= 1)
                {
                    dc.DrawText(slot.Bright, origin);
                }
                else if (progress > 0)
                {
                    dc.PushOpacityMask(BuildMask(slot, progress, feather));
                    dc.DrawText(slot.Bright, origin);
                    dc.Pop();
                }
            }
        }

        dc.Pop();
    }

    private static double WordProgress(LyricWord word, TimeSpan position)
    {
        if (position >= word.End)
            return 1;
        if (position <= word.Start)
            return 0;

        return (position - word.Start).TotalSeconds / (word.End - word.Start).TotalSeconds;
    }

    /// <summary>A horizontal opacity ramp whose soft leading edge sweeps across the word as progress goes 0 to 1.</summary>
    private static Brush BuildMask(WordSlot slot, double progress, double feather)
    {
        var width = Math.Max(slot.Width, 1);
        var f = Math.Min(feather, width * 0.6);
        var edge = progress * (width + f);

        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(slot.X, 0),
            EndPoint = new Point(slot.X + width, 0),
        };

        foreach (var x in new[] { 0, Math.Clamp(edge - f, 0, width), Math.Clamp(edge, 0, width), width }.Distinct().OrderBy(v => v))
        {
            var alpha = Math.Clamp((edge - x) / f, 0, 1);
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Round(alpha * 255), 0, 0, 0), x / width));
        }

        return brush;
    }

    private LineLayout GetLayout(LyricsResult lyrics, int index, bool next)
    {
        if (!_layouts.TryGetValue((index, next), out var layout))
        {
            layout = BuildLayout(lyrics.Lines[index].Words, next ? NextScale : 1.0);
            _layouts[(index, next)] = layout;
        }

        return layout;
    }

    private LineLayout BuildLayout(IReadOnlyList<LyricWord> words, double scale)
    {
        var style = _style!;
        var size = style.Size * scale;
        var rowHeight = size * RowHeightFactor;
        var maxWidth = Math.Max(100, ActualWidth - 2 * SidePadding);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var culture = CultureInfo.CurrentUICulture;

        FormattedText Make(string text, Brush brush) =>
            new(text, culture, FlowDirection.LeftToRight, style.Typeface, size, brush, dpi);

        var layout = new LineLayout();
        var row = new Row { Y = 0 };
        var x = 0.0;

        void FinishRow()
        {
            if (row.Slots.Count == 0)
                return;

            var last = row.Slots[^1];
            var shift = SidePadding + (maxWidth - (last.X + last.Width)) / 2;
            foreach (var slot in row.Slots)
                slot.X += shift;
            layout.Rows.Add(row);
        }

        foreach (var word in words)
        {
            var dim = Make(word.Text, style.Base);
            var visible = dim.Width;

            if (x > 0 && x + visible > maxWidth)
            {
                FinishRow();
                row = new Row { Y = row.Y + rowHeight };
                x = 0;
            }

            row.Slots.Add(new WordSlot
            {
                Word = word,
                X = x,
                Width = visible,
                Shadow = Make(word.Text, style.Shadow),
                Dim = dim,
                Bright = Make(word.Text, style.Highlight),
            });
            x += dim.WidthIncludingTrailingWhitespace;
        }

        FinishRow();
        layout.Height = layout.Rows.Count * rowHeight;
        return layout;
    }
}
