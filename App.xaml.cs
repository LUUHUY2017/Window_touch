using System.Threading;
using System.Windows;

namespace IDE_Touch_Window;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const string InstanceMutexName = @"Local\IDE_Touch_Window.SingleInstance";

    private Mutex? _instanceMutex;
    private TrayIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out bool isFirstInstance);

        if (!isFirstInstance)
        {
            // Đã có tiến trình đang chạy — thoát ngay để tránh nhân đôi nhân vật
            // khi người dùng bấm mở lại lúc autostart đã chạy sẵn.
            _instanceMutex.Dispose();
            _instanceMutex = null;
            Shutdown();
            return;
        }

        base.OnStartup(e);

        StartupManager.ApplyFirstRunDefault();

        MainWindow window = new MainWindow();
        MainWindow = window;
        _trayIcon = new TrayIcon(window);
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();

        if (_instanceMutex is not null)
        {
            _instanceMutex.ReleaseMutex();
            _instanceMutex.Dispose();
            _instanceMutex = null;
        }

        base.OnExit(e);
    }
}
