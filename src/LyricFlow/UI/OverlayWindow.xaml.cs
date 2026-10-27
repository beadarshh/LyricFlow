using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using LyricFlow.Lyrics;
using LyricFlow.Media;
using LyricFlow.Settings;

namespace LyricFlow.UI;

public partial class OverlayWindow : Window
{
    private readonly SettingsStore _store;
    private bool _moveMode;

    /// <summary>True while the window fills a whole screen (a Black or Custom background with "cover screen" on).</summary>
    private bool _covering;

    /// <summary>Work area (in DIPs) the overlay is centred on after being opened with the hotkey; null otherwise.</summary>
    private Rect? _centerArea;

    public OverlayWindow(PlaybackClock clock, SettingsStore store)
    {
        _store = store;
        InitializeComponent();
        View.Bind(clock, store);

        Width = DesiredWidth();
        PlaceFromSettings();

        SourceInitialized += (_, _) => ApplyWindowStyles();
        SizeChanged += OnSizeChanged;
        store.Changed += OnSettingsChanged;
    }

    public bool MoveMode => _moveMode;

    private bool WantsCover => _store.Current.Background != BackgroundMode.Transparent && _store.Current.CoverScreen;

    public void SetSession(bool hasSession) => View.SetSession(hasSession);

    public void SetPlaying(bool isPlaying) => View.SetPlaying(isPlaying);

    public void SetLyrics(LyricsResult? lyrics) => View.SetLyrics(lyrics);

    /// <summary>
    /// Shows the overlay. A covering overlay fills the screen the mouse is on; otherwise, with
    /// <paramref name="centerOnShow"/>, it opens in the middle of that screen.
    /// </summary>
    public void Present(bool centerOnShow)
    {
        if (IsVisible)
            return;

        _centerArea = null;
        ApplyLayoutMode();

        if (_covering)
        {
            // Take focus so Esc works; the user asked for a full-screen display.
            Show();
            Activate();
        }
        else if (centerOnShow)
        {
            Show();
            UpdateLayout();
            CenterOnCursorScreen();
        }
        else
        {
            PlaceFromSettings();
            Show();
        }

        View.NotifyShown();
    }

    public void ToggleVisible(bool centerOnShow)
    {
        if (IsVisible)
            Hide();
        else
            Present(centerOnShow);

        _store.Update(s => s.Visible = IsVisible);
        _store.Save();
    }

    /// <summary>Unlocking makes the overlay draggable (and never full-screen); locking saves where it was left.</summary>
    public void SetMoveMode(bool on)
    {
        _moveMode = on;
        MoveFrame.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        View.SetMoveMode(on);

        if (on)
        {
            _centerArea = null;
            ApplyLayoutMode();
            if (!IsVisible)
                Show();
        }
        else
        {
            _store.Update(s =>
            {
                s.Left = Left;
                s.Top = Top;
                s.Visible = IsVisible;
            });
            _store.Save();
            ApplyLayoutMode();
        }

        ApplyWindowStyles();
    }

    public void ResetPosition()
    {
        _centerArea = null;
        _store.Update(s =>
        {
            s.Left = null;
            s.Top = null;
        });
        if (!_covering)
            PlaceFromSettings();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_moveMode)
            DragMove();
        else if (_covering && e.ClickCount == 2)
            ToggleVisible(false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_covering && e.Key == Key.Escape)
            ToggleVisible(false);
    }

    private double DesiredWidth() =>
        Math.Clamp(_store.Current.OverlayWidth, 300, Math.Max(300, SystemParameters.VirtualScreenWidth));

    private void OnSettingsChanged()
    {
        // Switching in or out of full-screen only matters while the window is up; otherwise it is applied on show.
        if (IsVisible && (WantsCover && !_moveMode) != _covering)
        {
            ApplyLayoutMode();
            return;
        }

        if (_covering)
            return;

        var width = DesiredWidth();
        if (Math.Abs(width - Width) < 0.5)
            return;

        // Grow and shrink around the middle so a centred overlay stays centred.
        var delta = width - Width;
        Width = width;
        if (_centerArea is Rect area)
            Center(area);
        else
            Left -= delta / 2;
    }

    /// <summary>Puts the window in full-screen or floating form to match the settings.</summary>
    private void ApplyLayoutMode()
    {
        var cover = WantsCover && !_moveMode;
        if (cover)
        {
            var bounds = CursorScreenArea(workArea: false);
            SizeToContent = SizeToContent.Manual;
            _covering = true;
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
            Focusable = true;
            View.SetFullscreen(true);
        }
        else if (_covering)
        {
            _covering = false;
            Focusable = false;
            View.SetFullscreen(false);
            SizeToContent = SizeToContent.Height;
            Width = DesiredWidth();
            PlaceFromSettings();
        }

        ApplyWindowStyles();
    }

    // The height depends on the style and font size, so the window can only be placed once it is measured.
    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_moveMode || _covering)
            return;

        if (_centerArea is Rect area)
        {
            Center(area);
            return;
        }

        if (!e.HeightChanged)
            return;

        if (_store.Current.Top is null)
        {
            Top = SystemParameters.WorkArea.Bottom - ActualHeight - 40;
            return;
        }

        var screenBottom = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
        if (Top + ActualHeight > screenBottom)
            Top = Math.Max(SystemParameters.VirtualScreenTop, screenBottom - ActualHeight);
    }

    /// <summary>The screen the mouse is on, in DIPs: its full bounds, or just the part not under the taskbar.</summary>
    private Rect CursorScreenArea(bool workArea)
    {
        // The handle (and so the DPI transform) must exist before the window is shown.
        new WindowInteropHelper(this).EnsureHandle();
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice;

        var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
        var pixels = workArea ? screen.WorkingArea : screen.Bounds;

        if (transform is System.Windows.Media.Matrix m)
            return new Rect(m.Transform(new Point(pixels.Left, pixels.Top)), m.Transform(new Point(pixels.Right, pixels.Bottom)));

        return workArea
            ? SystemParameters.WorkArea
            : new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
    }

    private void CenterOnCursorScreen()
    {
        var area = CursorScreenArea(workArea: true);
        _centerArea = area;
        Center(area);
    }

    private void Center(Rect area)
    {
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + (area.Height - ActualHeight) / 2;
    }

    private void PlaceFromSettings()
    {
        var settings = _store.Current;
        var work = SystemParameters.WorkArea;

        if (settings.Left is double left && settings.Top is double top && IsOnScreen(left, top))
        {
            Left = left;
            Top = top;
        }
        else
        {
            Left = work.Left + (work.Width - Width) / 2;
            Top = work.Bottom - (ActualHeight > 0 ? ActualHeight : 220) - 40;
        }
    }

    private bool IsOnScreen(double left, double top)
    {
        // The saved spot may be on a monitor that has since been unplugged.
        var screen = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        return screen.Contains(new Point(left + Width / 2, top + 20));
    }

    /// <summary>
    /// A floating overlay is click-through and never takes focus. A full-screen one takes clicks
    /// and keys (double-click or Esc hides it) so it can't swallow input invisibly.
    /// </summary>
    private void ApplyWindowStyles()
    {
        var hwnd = new WindowInteropHelper(this).EnsureHandle();

        var clickThrough = !_moveMode && !_covering;
        var style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        style |= NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_TOOLWINDOW;
        style = _covering
            ? style & ~NativeMethods.WS_EX_NOACTIVATE
            : style | NativeMethods.WS_EX_NOACTIVATE;
        style = clickThrough
            ? style | NativeMethods.WS_EX_TRANSPARENT
            : style & ~NativeMethods.WS_EX_TRANSPARENT;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, style);
    }
}
