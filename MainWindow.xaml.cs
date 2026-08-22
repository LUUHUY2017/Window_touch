using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    public MainWindow()
    {
        InitializeComponent();
        LoadIconImage();
    }

    private void LoadIconImage()
    {
        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.png");
            if (File.Exists(iconPath))
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                IconImageBrush.ImageSource = bitmap;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load icon: {ex.Message}");
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

        // Ẩn cửa sổ tạm thời để không chụp phải chính nút nổi
        this.Opacity = 0;
        await Task.Delay(250);

        try
        {
            int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            int screenHeight = (int)SystemParameters.PrimaryScreenHeight;

            using (var bitmap = new System.Drawing.Bitmap(screenWidth, screenHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (var g = System.Drawing.Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(screenWidth, screenHeight), System.Drawing.CopyPixelOperation.SourceCopy);
                }

                // Lưu ảnh vào Pictures/Screenshots
                string picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                string screenshotsFolder = Path.Combine(picturesPath, "Screenshots");
                if (!Directory.Exists(screenshotsFolder))
                {
                    Directory.CreateDirectory(screenshotsFolder);
                }

                string fileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string fullPath = Path.Combine(screenshotsFolder, fileName);
                bitmap.Save(fullPath, System.Drawing.Imaging.ImageFormat.Png);

                // Sao chép ảnh vào Clipboard
                IntPtr hBitmap = bitmap.GetHbitmap();
                try
                {
                    BitmapSource wpfBitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    Clipboard.SetImage(wpfBitmap);
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }

            ShowToast($"📷 Đã lưu: {DateTime.Now:HH:mm:ss}");
        }
        catch (Exception ex)
        {
            ShowToast($"❌ Lỗi: {ex.Message}");
        }
        finally
        {
            this.Opacity = 1;
        }
    }

    private void BtnDesktop_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        try
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = desktopPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowToast($"❌ Lỗi: {ex.Message}");
        }
    }

    private void BtnExit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
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