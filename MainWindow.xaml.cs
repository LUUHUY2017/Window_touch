using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace IDE_Touch_Window;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Point _startPoint;
    private bool _isDragging = false;
    private bool _isMenuOpen = false;

    // Raw pixel data for pixel-perfect alpha hit testing
    private byte[]? _iconPixelData;
    private int _iconPixelWidth;
    private int _iconPixelHeight;

    private Storyboard? _idleAnimationStoryboard;
    private DispatcherTimer? _idleSurpriseTimer;

    private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    private string? _cachedWeatherMsg;
    private int _msgIndex = 0;
    private CancellationTokenSource? _typewriterCts;

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

    private const int WM_NCHITTEST = 0x0084;
    private static readonly IntPtr HTTRANSPARENT = (IntPtr)(-1);
    private static readonly IntPtr HTCLIENT = (IntPtr)1;

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
        StartIdleAnimation();
        SetupIdleSurpriseTimer();
        _ = FetchWeatherAsync();

        // Trigger welcome chat bubble 2 seconds after launch so user sees typewriter effect immediately!
        Task.Delay(2000).ContinueWith(_ =>
        {
            Dispatcher.Invoke(() =>
            {
                ShowChatBubble("🌸 Xin chào Anh Huy, Anh còn ở đó không?");
            });
        });
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;

        // Apply WS_EX_TOOLWINDOW extended style
        int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TOOLWINDOW);

        // Hook WndProc for pixel-perfect transparency hit testing
        HwndSource? source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);
    }

    private async Task FetchWeatherAsync()
    {
        try
        {
            // Hanoi coordinates: 21.0285, 105.8542
            string url = "https://api.open-meteo.com/v1/forecast?latitude=21.0285&longitude=105.8542&current_weather=true";
            string json = await _httpClient.GetStringAsync(url);
            
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement current = doc.RootElement.GetProperty("current_weather");
            double temp = current.GetProperty("temperature").GetDouble();
            int code = current.GetProperty("weathercode").GetInt32();
            
            string weatherDesc = code switch
            {
                0 => "Trời quang đãng ☀️",
                1 or 2 => "Trời ít mây 🌤️",
                3 => "Trời nhiều mây ⛅",
                45 or 48 => "Có sương mù 🌫️",
                51 or 53 or 61 or 63 or 80 => "Có mưa rào 🌧️",
                95 or 96 => "Trời dông 🌩️",
                _ => "Thời tiết dịu mát 🍃"
            };

            _cachedWeatherMsg = $"🌤️ Thời tiết Hà Nội: {temp:F1}°C, {weatherDesc}";
        }
        catch
        {
            _cachedWeatherMsg = null;
        }
    }

    private void StartIdleAnimation()
    {
        DoubleAnimation bounceAnim = new DoubleAnimation
        {
            From = 0,
            To = -8,
            Duration = TimeSpan.FromSeconds(1.3),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        DoubleAnimation breatheScaleY = new DoubleAnimation
        {
            From = 1.0,
            To = 1.025,
            Duration = TimeSpan.FromSeconds(1.3),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        DoubleAnimation breatheScaleX = new DoubleAnimation
        {
            From = 1.0,
            To = 0.98,
            Duration = TimeSpan.FromSeconds(1.3),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

        Storyboard.SetTarget(bounceAnim, IconTranslateTransform);
        Storyboard.SetTargetProperty(bounceAnim, new PropertyPath(TranslateTransform.YProperty));

        Storyboard.SetTarget(breatheScaleY, IconScaleTransform);
        Storyboard.SetTargetProperty(breatheScaleY, new PropertyPath(ScaleTransform.ScaleYProperty));

        Storyboard.SetTarget(breatheScaleX, IconScaleTransform);
        Storyboard.SetTargetProperty(breatheScaleX, new PropertyPath(ScaleTransform.ScaleXProperty));

        _idleAnimationStoryboard = new Storyboard();
        _idleAnimationStoryboard.Children.Add(bounceAnim);
        _idleAnimationStoryboard.Children.Add(breatheScaleY);
        _idleAnimationStoryboard.Children.Add(breatheScaleX);
        _idleAnimationStoryboard.Begin();
    }

    private void SetupIdleSurpriseTimer()
    {
        _idleSurpriseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _idleSurpriseTimer.Tick += (s, e) =>
        {
            if (!_isMenuOpen && !_isDragging)
            {
                PlayIdleSurpriseJump();
                ShowChatBubble(GetNextIdleMessage());
            }
        };
        _idleSurpriseTimer.Start();
    }

    private string GetNextIdleMessage()
    {
        List<string> messages = new List<string>
        {
            "🌸 Xin chào Anh Huy, Anh còn ở đó không?",
            "☕ Anh Huy ơi, làm việc nhớ nghỉ tay uống chút nước nhé!",
            "💡 Mẹo mắt: Hãy nhìn ra xa 20 feet trong 20 giây để thư giãn mắt nha!",
            "🚗 Giao thông: Đường xá hôm nay khá thông thoáng, chúc anh di chuyển an toàn!",
            "💻 Code Tip: Commit code thường xuyên để giữ tiến độ thật tốt nhé anh Huy!",
            "✨ Chúc Anh Huy một ngày làm việc tràn đầy năng lượng và sáng tạo!"
        };

        if (!string.IsNullOrEmpty(_cachedWeatherMsg))
        {
            messages.Insert(1, _cachedWeatherMsg);
        }

        string msg = messages[_msgIndex % messages.Count];
        _msgIndex++;
        return msg;
    }

    private async void ShowChatBubble(string message)
    {
        _typewriterCts?.Cancel();
        _typewriterCts = new CancellationTokenSource();
        var token = _typewriterCts.Token;

        ChatText.Text = "";
        ChatBubble.Visibility = Visibility.Visible;

        DoubleAnimation scaleAnim = new DoubleAnimation(0.3, 1.0, TimeSpan.FromMilliseconds(250))
        {
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 }
        };
        DoubleAnimation fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));

        ChatBubbleScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        ChatBubbleScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        ChatBubble.BeginAnimation(UIElement.OpacityProperty, fadeIn);

        try
        {
            // Typewriter effect: type out character by character
            for (int i = 1; i <= message.Length; i++)
            {
                if (token.IsCancellationRequested) return;
                ChatText.Text = message.Substring(0, i);
                await Task.Delay(35, token);
            }

            // Wait 10 seconds after full text is typed so user can read comfortably
            await Task.Delay(10000, token);

            if (token.IsCancellationRequested) return;

            // Smooth fade out
            DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400));
            fadeOut.Completed += (s, e) =>
            {
                if (!token.IsCancellationRequested)
                {
                    ChatBubble.Visibility = Visibility.Collapsed;
                }
            };
            ChatBubble.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
        catch (TaskCanceledException)
        {
            // Dismissed by click or overridden by new message
        }
    }

    private void ChatBubble_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _typewriterCts?.Cancel();

        DoubleAnimation fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
        fadeOut.Completed += (s, e) =>
        {
            ChatBubble.Visibility = Visibility.Collapsed;
        };
        ChatBubble.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    private void PlayIdleSurpriseJump()
    {
        // Double hop / surprise pop jump animation when left idle
        DoubleAnimationUsingKeyFrames jumpKeyFrames = new DoubleAnimationUsingKeyFrames();
        
        // Keyframe 1: First Hop
        jumpKeyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        jumpKeyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(-24, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))) 
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        jumpKeyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))) 
            { EasingFunction = new BounceEase { Bounces = 1, Bounciness = 2, EasingMode = EasingMode.EaseOut } });
        
        // Keyframe 2: Second Hop (Cute accent)
        jumpKeyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(-12, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(450))) 
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
        jumpKeyFrames.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(580))) 
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });

        // Squish & Stretch Keyframes
        DoubleAnimationUsingKeyFrames scaleYKeyFrames = new DoubleAnimationUsingKeyFrames();
        scaleYKeyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        scaleYKeyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(1.12, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
        scaleYKeyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.92, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))));
        scaleYKeyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(1.06, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(450))));
        scaleYKeyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(580))));

        IconTranslateTransform.BeginAnimation(TranslateTransform.YProperty, jumpKeyFrames);
        IconScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYKeyFrames);
    }

    private void PlayClickJumpAnimation()
    {
        DoubleAnimation jumpAnim = new DoubleAnimation
        {
            From = 0,
            To = -20,
            Duration = TimeSpan.FromMilliseconds(180),
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        IconTranslateTransform.BeginAnimation(TranslateTransform.YProperty, jumpAnim);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_NCHITTEST)
        {
            int x = (short)(lParam.ToInt32() & 0xFFFF);
            int y = (short)((lParam.ToInt32() >> 16) & 0xFFFF);
            Point screenPt = new Point(x, y);
            Point windowPt = PointFromScreen(screenPt);

            // 1. If sub-menu is open or ChatBubble/Toast is visible, check if mouse is over them
            HitTestResult hitResult = VisualTreeHelper.HitTest(MainGrid, windowPt);
            if (hitResult != null && hitResult.VisualHit != null)
            {
                DependencyObject? dObj = hitResult.VisualHit;
                while (dObj != null && dObj != MainGrid)
                {
                    if (dObj is Button || dObj == ToastNotification || dObj == ChatBubble)
                    {
                        handled = true;
                        return HTCLIENT;
                    }
                    dObj = VisualTreeHelper.GetParent(dObj);
                }
            }

            // 2. Check if mouse is over an opaque pixel of the anime girl PNG
            if (IsPixelOpaqueAtWindowPoint(windowPt))
            {
                handled = true;
                return HTCLIENT;
            }

            // 3. Transparent area -> Return HTTRANSPARENT to pass click straight through to Desktop / Window beneath!
            handled = true;
            return HTTRANSPARENT;
        }

        return IntPtr.Zero;
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

                    // Extract raw pixel data for alpha hit testing
                    FormatConvertedBitmap bgraBitmap = new FormatConvertedBitmap();
                    bgraBitmap.BeginInit();
                    bgraBitmap.Source = bitmap;
                    bgraBitmap.DestinationFormat = PixelFormats.Bgra32;
                    bgraBitmap.EndInit();

                    _iconPixelWidth = bgraBitmap.PixelWidth;
                    _iconPixelHeight = bgraBitmap.PixelHeight;
                    int stride = _iconPixelWidth * 4;
                    _iconPixelData = new byte[_iconPixelHeight * stride];
                    bgraBitmap.CopyPixels(_iconPixelData, stride, 0);
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

    private bool IsPixelOpaqueAtWindowPoint(Point windowPt)
    {
        if (_iconPixelData == null || _iconPixelWidth == 0 || _iconPixelHeight == 0)
            return false;

        Point imgPt;
        try
        {
            imgPt = this.TranslatePoint(windowPt, IconImage);
        }
        catch
        {
            return false;
        }

        double actualWidth = IconImage.ActualWidth;
        double actualHeight = IconImage.ActualHeight;

        if (actualWidth <= 0 || actualHeight <= 0)
            return false;

        if (imgPt.X < 0 || imgPt.X >= actualWidth || imgPt.Y < 0 || imgPt.Y >= actualHeight)
            return false;

        int pixelX = (int)(imgPt.X * _iconPixelWidth / actualWidth);
        int pixelY = (int)(imgPt.Y * _iconPixelHeight / actualHeight);

        pixelX = Math.Clamp(pixelX, 0, _iconPixelWidth - 1);
        pixelY = Math.Clamp(pixelY, 0, _iconPixelHeight - 1);

        int stride = _iconPixelWidth * 4;
        int alphaIndex = pixelY * stride + pixelX * 4 + 3;

        if (alphaIndex >= 0 && alphaIndex < _iconPixelData.Length)
        {
            return _iconPixelData[alphaIndex] > 30; // Alpha threshold for non-transparent pixels
        }

        return false;
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
            PlayClickJumpAnimation();
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
