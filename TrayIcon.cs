using System;
using System.Drawing;
using System.Windows;
using System.Windows.Resources;
using WinForms = System.Windows.Forms;

namespace IDE_Touch_Window;

/// <summary>
/// Icon khay hệ thống (system tray) kèm menu chuột phải.
/// Cửa sổ chính chạy ở chế độ ToolWindow nên không có nút trên taskbar —
/// đây là nơi duy nhất để hiện/ẩn nhân vật, bật/tắt autostart và thoát ứng dụng.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly Window _target;
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly WinForms.ToolStripMenuItem _visibilityItem;
    private readonly WinForms.ToolStripMenuItem _startupItem;
    private bool _disposed;

    public TrayIcon(Window target)
    {
        _target = target;

        _visibilityItem = new WinForms.ToolStripMenuItem("Ẩn nhân vật", null, (_, _) => ToggleVisibility());
        _startupItem = new WinForms.ToolStripMenuItem("Khởi động cùng Windows", null, (_, _) => ToggleStartup())
        {
            Checked = StartupManager.IsEnabled()
        };

        WinForms.ContextMenuStrip menu = new();
        menu.Items.Add(_visibilityItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(new WinForms.ToolStripMenuItem("Thoát", null, (_, _) => Application.Current.Shutdown()));

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "IDE Touch Window",
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
            {
                ToggleVisibility();
            }
        };
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            StreamResourceInfo? info = Application.GetResourceStream(new Uri("pack://application:,,,/app.ico"));
            if (info is not null)
            {
                using System.IO.Stream stream = info.Stream;
                return new Icon(stream, WinForms.SystemInformation.SmallIconSize);
            }
        }
        catch
        {
            // Rơi về icon hệ thống nếu resource lỗi — tray vẫn phải hiện được.
        }

        return SystemIcons.Application;
    }

    private void ToggleVisibility()
    {
        if (_target.IsVisible)
        {
            _target.Hide();
            _visibilityItem.Text = "Hiện nhân vật";
        }
        else
        {
            _target.Show();
            _target.Topmost = true;
            _visibilityItem.Text = "Ẩn nhân vật";
        }
    }

    private void ToggleStartup()
    {
        bool enable = !StartupManager.IsEnabled();

        if (StartupManager.SetEnabled(enable, out string? error))
        {
            _startupItem.Checked = enable;
            ShowBalloon(enable
                ? "Đã bật khởi động cùng Windows."
                : "Đã tắt khởi động cùng Windows.");
        }
        else
        {
            _startupItem.Checked = StartupManager.IsEnabled();
            ShowBalloon($"Không đổi được thiết lập: {error}");
        }
    }

    private void ShowBalloon(string message)
    {
        _notifyIcon.BalloonTipTitle = "IDE Touch Window";
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.ShowBalloonTip(3000);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
    }
}
