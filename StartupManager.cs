using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace IDE_Touch_Window;

/// <summary>
/// Quản lý việc tự khởi động cùng Windows thông qua khoá Run của người dùng hiện tại
/// (HKCU) — không cần quyền Administrator.
/// </summary>
internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "IDE_Touch_Window";

    /// <summary>
    /// Đường dẫn tới file .exe đang chạy. Với bản publish single-file thì
    /// Environment.ProcessPath trả về chính file .exe chứ không phải dll tạm.
    /// </summary>
    public static string ExecutablePath
    {
        get
        {
            string? path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                return path;
            }

            using Process current = Process.GetCurrentProcess();
            return current.MainModule?.FileName ?? string.Empty;
        }
    }

    private static string CommandLine => $"\"{ExecutablePath}\"";

    private static string MarkerPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IDE_Touch_Window",
        "startup.configured");

    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    public static bool SetEnabled(bool enable, out string? error)
    {
        error = null;
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
                ?? throw new InvalidOperationException("Không mở được khoá registry Run.");

            if (enable)
            {
                key.SetValue(ValueName, CommandLine, RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Nếu đã bật autostart mà file .exe được chuyển sang thư mục khác thì cập nhật lại
    /// đường dẫn trong registry, tránh việc Windows khởi động một file không còn tồn tại.
    /// </summary>
    private static void SyncPathIfEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(ValueName) is not string current)
            {
                return;
            }

            if (!string.Equals(current, CommandLine, StringComparison.OrdinalIgnoreCase))
            {
                key.SetValue(ValueName, CommandLine, RegistryValueKind.String);
            }
        }
        catch
        {
            // Không chặn quá trình khởi động chỉ vì đồng bộ registry thất bại.
        }
    }

    /// <summary>
    /// Lần chạy đầu tiên thì bật autostart mặc định. Các lần sau chỉ đồng bộ đường dẫn,
    /// nhờ vậy lựa chọn tắt autostart của người dùng không bị ghi đè.
    /// </summary>
    public static void ApplyFirstRunDefault()
    {
        try
        {
            if (File.Exists(MarkerPath))
            {
                SyncPathIfEnabled();
                return;
            }

            SetEnabled(true, out _);

            string? folder = Path.GetDirectoryName(MarkerPath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(MarkerPath, DateTime.Now.ToString("O"));
            }
        }
        catch
        {
            // Autostart là tính năng phụ — lỗi ở đây không được làm hỏng việc mở ứng dụng.
        }
    }
}
