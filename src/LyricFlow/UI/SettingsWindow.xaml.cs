using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LyricFlow.Settings;

namespace LyricFlow.UI;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _store;
    private readonly HotkeyService _hotkey;
    private readonly Action _resetPosition;
    private bool _loading = true;

    public SettingsWindow(SettingsStore store, HotkeyService hotkey, Action resetPosition)
    {
        _store = store;
        _hotkey = hotkey;
        _resetPosition = resetPosition;
        InitializeComponent();

        StyleBox.ItemsSource = Enum.GetValues<OverlayStyle>();
        BackgroundModeBox.ItemsSource = Enum.GetValues<BackgroundMode>();
        FontBox.ItemsSource = Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(n => n).ToList();

        var s = store.Current;
        StyleBox.SelectedItem = s.Style;
        FontBox.SelectedItem = s.FontFamily;
        SizeSlider.Value = s.FontSize;
        WidthSlider.Value = s.OverlayWidth;
        BackgroundModeBox.SelectedItem = s.Background;
        BackgroundBox.Text = s.BackgroundColor;
        OpacitySlider.Value = s.Opacity;
        OffsetSlider.Value = s.OffsetMs;
        HotkeyBox.Text = s.Hotkey;
        CenterBox.IsChecked = s.CenterOnHotkey;
        CoverBox.IsChecked = s.CoverScreen;
        BlockBox.Text = s.KineticBlockColor;
        WordBox.Text = s.KineticWordColor;
        PerPageSlider.Value = s.KineticWordsPerPage;
        TiltSlider.Value = s.KineticTilt;
        VarietySlider.Value = s.KineticSizeVariety;
        PopBox.IsChecked = s.KineticPop;
        BaseBox.Text = s.BaseColor;
        HighlightBox.Text = s.HighlightColor;
        MiniBox.IsChecked = s.MiniMode;

        _loading = false;
        UpdateLabels();
        UpdateBackgroundControls();
        TryColor(BackgroundBox.Text, BackgroundSwatch);
        TryColor(BlockBox.Text, BlockSwatch);
        TryColor(WordBox.Text, WordSwatch);
        TryColor(BaseBox.Text, BaseSwatch);
        TryColor(HighlightBox.Text, HighlightSwatch);
        Closed += (_, _) => _store.Save();
    }

    private void OnChanged(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;

        UpdateLabels();
        UpdateBackgroundControls();

        var backgroundOk = TryColor(BackgroundBox.Text, BackgroundSwatch);
        var blockOk = TryColor(BlockBox.Text, BlockSwatch);
        var wordOk = TryColor(WordBox.Text, WordSwatch);
        var baseOk = TryColor(BaseBox.Text, BaseSwatch);
        var highlightOk = TryColor(HighlightBox.Text, HighlightSwatch);

        _store.Update(s =>
        {
            if (StyleBox.SelectedItem is OverlayStyle style)
                s.Style = style;
            if (FontBox.SelectedItem is string font)
                s.FontFamily = font;
            s.FontSize = Math.Round(SizeSlider.Value);
            s.OverlayWidth = Math.Round(WidthSlider.Value);
            if (BackgroundModeBox.SelectedItem is BackgroundMode mode)
                s.Background = mode;
            if (backgroundOk)
                s.BackgroundColor = BackgroundBox.Text.Trim();
            s.Opacity = Math.Round(OpacitySlider.Value, 2);
            s.OffsetMs = (int)OffsetSlider.Value;
            s.CenterOnHotkey = CenterBox.IsChecked == true;
            s.CoverScreen = CoverBox.IsChecked == true;
            if (blockOk)
                s.KineticBlockColor = BlockBox.Text.Trim();
            if (wordOk)
                s.KineticWordColor = WordBox.Text.Trim();
            s.KineticWordsPerPage = (int)Math.Round(PerPageSlider.Value);
            s.KineticTilt = Math.Round(TiltSlider.Value, 1);
            s.KineticSizeVariety = Math.Round(VarietySlider.Value, 2);
            s.KineticPop = PopBox.IsChecked == true;
            if (baseOk)
                s.BaseColor = BaseBox.Text.Trim();
            if (highlightOk)
                s.HighlightColor = HighlightBox.Text.Trim();
            s.MiniMode = MiniBox.IsChecked == true;
        });
    }

    /// <summary>The colour box and picker only matter for a custom background; the swatch shows what is drawn.</summary>
    private void UpdateBackgroundControls()
    {
        var mode = BackgroundModeBox.SelectedItem is BackgroundMode m ? m : _store.Current.Background;
        var custom = mode == BackgroundMode.Custom;
        BackgroundBox.IsEnabled = custom;
        PickBackgroundButton.IsEnabled = custom;
        BackgroundSwatch.Opacity = custom ? 1 : 0.35;
    }

    private void OnPickBackground(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, AnyColor = true };
        if (TryParseColor(BackgroundBox.Text) is Color current)
            dialog.Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B);

        // Owned by this window so it stays above it even when a full-screen overlay is topmost.
        var owner = new System.Windows.Forms.NativeWindow();
        owner.AssignHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle);
        try
        {
            if (dialog.ShowDialog(owner) != System.Windows.Forms.DialogResult.OK)
                return;
        }
        finally
        {
            owner.ReleaseHandle();
        }

        // The dialog picks a solid colour; edit the text to add see-through (#AARRGGBB).
        var picked = dialog.Color;
        BackgroundBox.Text = $"#{picked.R:X2}{picked.G:X2}{picked.B:X2}";
    }

    private static Color? TryParseColor(string text)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(text.Trim());
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private void UpdateLabels()
    {
        SizeLabel.Text = Math.Round(SizeSlider.Value).ToString(CultureInfo.InvariantCulture);
        WidthLabel.Text = Math.Round(WidthSlider.Value).ToString(CultureInfo.InvariantCulture);
        OpacityLabel.Text = $"{Math.Round(OpacitySlider.Value * 100)}%";
        OffsetLabel.Text = ((int)OffsetSlider.Value).ToString("+0;-0;0", CultureInfo.InvariantCulture);
        PerPageLabel.Text = Math.Round(PerPageSlider.Value).ToString(CultureInfo.InvariantCulture);
        TiltLabel.Text = $"{TiltSlider.Value:0.#}°";
        VarietyLabel.Text = $"{Math.Round(VarietySlider.Value * 100)}%";
    }

    private static bool TryColor(string text, Border swatch)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(text.Trim());
            swatch.Background = new SolidColorBrush(color);
            return true;
        }
        catch (FormatException)
        {
            swatch.Background = Brushes.Transparent;
            return false;
        }
    }

    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (!HotkeyParser.IsSupportedKey(virtualKey))
            return;

        uint modifiers = 0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyParser.Control;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyParser.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyParser.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= HotkeyParser.Win;
        if (modifiers == 0)
        {
            StatusText.Text = "Include Ctrl, Alt, Shift or Win.";
            return;
        }

        var text = HotkeyParser.Format(modifiers, virtualKey);
        if (_hotkey.Register(text))
        {
            HotkeyBox.Text = text;
            StatusText.Text = string.Empty;
            _store.Update(s => s.Hotkey = text);
        }
        else
        {
            // Registration replaced the old hotkey, so restore it.
            _hotkey.Register(_store.Current.Hotkey);
            StatusText.Text = $"{text} is already in use.";
        }
    }

    private void OnResetPosition(object sender, RoutedEventArgs e) => _resetPosition();
}
