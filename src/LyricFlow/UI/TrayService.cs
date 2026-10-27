using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using WinForms = System.Windows.Forms;

namespace LyricFlow.UI;

public sealed class TrayService : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ToolStripMenuItem _showItem;
    private readonly WinForms.ToolStripMenuItem _moveItem;
    private readonly WinForms.ContextMenuStrip _menu;
    private readonly Icon _drawnIcon;

    public TrayService()
    {
        _drawnIcon = DrawIcon();

        _showItem = new WinForms.ToolStripMenuItem("Show lyrics") { CheckOnClick = true };
        _moveItem = new WinForms.ToolStripMenuItem("Move overlay") { CheckOnClick = true };
        var settingsItem = new WinForms.ToolStripMenuItem("Settings...");
        var quitItem = new WinForms.ToolStripMenuItem("Quit");

        _showItem.Click += (_, _) => ToggleVisibleRequested?.Invoke();
        _moveItem.Click += (_, _) => MoveModeRequested?.Invoke(_moveItem.Checked);
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke();
        quitItem.Click += (_, _) => QuitRequested?.Invoke();

        _menu = new WinForms.ContextMenuStrip();
        _menu.Items.Add(_showItem);
        _menu.Items.Add(_moveItem);
        _menu.Items.Add(settingsItem);
        _menu.Items.Add(new WinForms.ToolStripSeparator());
        _menu.Items.Add(quitItem);

        _icon = new WinForms.NotifyIcon
        {
            Text = "LyricFlow",
            Icon = _drawnIcon,
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => SettingsRequested?.Invoke();
    }

    public event Action? ToggleVisibleRequested;

    public event Action<bool>? MoveModeRequested;

    public event Action? SettingsRequested;

    public event Action? QuitRequested;

    public void SetOverlayVisible(bool visible) => _showItem.Checked = visible;

    public void SetMoveMode(bool on) => _moveItem.Checked = on;

    private static Icon DrawIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var fill = new LinearGradientBrush(new Rectangle(0, 0, 32, 32), Color.FromArgb(0x8E, 0x6B, 0xFF), Color.FromArgb(0xFF, 0x6B, 0xB5), 45f);
            g.FillEllipse(fill, 1, 1, 30, 30);
            using var font = new Font("Segoe UI Symbol", 17f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("♪", font, Brushes.White, new RectangleF(0, 1, 32, 32), format);
        }

        var handle = bitmap.GetHicon();
        try
        {
            // Clone so the icon owns its data and the GDI handle can be released.
            return (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _drawnIcon.Dispose();
    }
}
