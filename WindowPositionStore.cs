using System;
using System.Globalization;
using System.IO;

namespace IDE_Touch_Window;

/// <summary>
/// Lưu vị trí nhân vật giữa các lần chạy. Ứng dụng khởi động cùng Windows và không có
/// nút trên taskbar, nên nếu mỗi lần bật lại nhảy về giữa màn hình thì rất khó chịu.
/// </summary>
internal static class WindowPositionStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "IDE_Touch_Window",
        "window-position.txt");

    public static bool TryLoad(out double left, out double top)
    {
        left = 0;
        top = 0;

        try
        {
            if (!File.Exists(FilePath))
            {
                return false;
            }

            string[] parts = File.ReadAllText(FilePath).Split(';');
            return parts.Length == 2
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out left)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out top);
        }
        catch
        {
            return false;
        }
    }

    public static void Save(double left, double top)
    {
        try
        {
            string? folder = Path.GetDirectoryName(FilePath);
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            Directory.CreateDirectory(folder);
            File.WriteAllText(
                FilePath,
                string.Format(CultureInfo.InvariantCulture, "{0:F2};{1:F2}", left, top));
        }
        catch
        {
            // Không cản trở thao tác kéo thả chỉ vì ghi file thất bại.
        }
    }
}
