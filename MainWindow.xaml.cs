using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace IDE_Touch_Window;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Point _startPoint;
    private bool _isDragging = false;
    private bool _isMenuOpen = false;

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private const uint KeyUpFlag = 0x0002;
    private const byte VirtualKeyControl = 0x11;
    private const byte VirtualKeyShift = 0x10;
    private const byte VirtualKeyS = 0x53;
    private const byte VirtualKeyWindows = 0x5B;
    private const byte VirtualKeyD = 0x44;

    public MainWindow()
    {
        InitializeComponent();
        LoadIconImage();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TOOLWINDOW);
    }

    private void LoadIconImage()
    {
        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon-transparent.png");
            if (!File.Exists(iconPath))
            {
                iconPath = Path.Combine(Directory.GetCurrentDirectory(), "icon-transparent.png");
            }

            if (File.Exists(iconPath))
            {
                byte[] bytes = File.ReadAllBytes(iconPath);
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = ms;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    IconImage.Source = bitmap;
                }
                FallbackText.Visibility = Visibility.Collapsed;
            }
            else
            {
                FallbackText.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi nạp ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #region Mouse Drag & Click Disambiguation

    private void MainIcon_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(this);
        _isDragging = false;
    }

    private void MainIcon_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
        {
            Point currentPoint = e.GetPosition(this);
            Vector diff = _startPoint - currentPoint;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _isDragging = true;
                this.DragMove();
            }
        }
    }

    private void MainIcon_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            ToggleMenu();
        }
        _isDragging = false;
    }

    #endregion

    #region Radial Menu Animations

    private void ToggleMenu()
    {
        if (!_isMenuOpen)
        {
            // Open Menu Animation
            MenuCanvas.Visibility = Visibility.Visible;
            
            DoubleAnimation scaleAnimation = new DoubleAnimation
            {
                From = 0.2,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 }
            };

            DoubleAnimation opacityAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200)
            };

            MenuScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            MenuScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
            MenuCanvas.BeginAnimation(Canvas.OpacityProperty, opacityAnimation);

            _isMenuOpen = true;
        }
        else
        {
            CloseMenu();
        }
    }

    private void CloseMenu()
    {
        if (!_isMenuOpen) return;

        DoubleAnimation scaleAnimation = new DoubleAnimation
        {
            From = 1.0,
            To = 0.2,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        DoubleAnimation opacityAnimation = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(150)
        };

        scaleAnimation.Completed += (s, e) =>
        {
            MenuCanvas.Visibility = Visibility.Collapsed;
        };

        MenuScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        MenuScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        MenuCanvas.BeginAnimation(Canvas.OpacityProperty, opacityAnimation);

        _isMenuOpen = false;
    }

    #endregion

    #region Feature Handlers

    private async void BtnScreenshot_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();

        // 1. Hide the window so it doesn't appear in the screenshot
        this.Hide();

        // 2. Wait for DWM render loop to clear the window from desktop surface
        await Task.Delay(300);

        // 3. Trigger Windows Screenshot tool
        try
        {
            Process.Start(new ProcessStartInfo("ms-screenclip:") { UseShellExecute = true });
        }
        catch
        {
            SendHotkey(VirtualKeyWindows, VirtualKeyShift, VirtualKeyS);
        }

        // 4. Wait for Snipping Tool overlay to initialize before re-showing floating window
        await Task.Delay(2000);
        this.Show();
        this.Topmost = true;
        ShowToast("📸 Đã mở công cụ chụp màn hình!");
    }

    private async void BtnDesktop_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();

        // Send Win + D to minimize all windows
        SendHotkey(VirtualKeyWindows, VirtualKeyD);

        // Wait brief delay then bring our floating widget back on top of Desktop
        await Task.Delay(200);
        this.WindowState = WindowState.Normal;
        this.Topmost = true;
        this.Activate();

        ShowToast("🖥️ Hiển thị Desktop!");
    }

    private void BtnCommandPrompt_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = true
            });
            ShowToast("💻 Đã mở Command Prompt!");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi mở CMD: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void SendHotkey(params byte[] keys)
    {
        foreach (byte key in keys)
        {
            keybd_event(key, 0, 0, UIntPtr.Zero);
        }

        for (int index = keys.Length - 1; index >= 0; index--)
        {
            keybd_event(keys[index], 0, KeyUpFlag, UIntPtr.Zero);
        }
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastNotification.Visibility = Visibility.Visible;

        DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
        DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400))
        {
            BeginTime = TimeSpan.FromSeconds(2)
        };

        fadeOut.Completed += (s, e) =>
        {
            ToastNotification.Visibility = Visibility.Collapsed;
        };

        ToastNotification.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        ToastNotification.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    #endregion
}
