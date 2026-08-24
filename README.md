# Window_touch

Ứng dụng Desktop nổi trên màn hình (**Floating Icon Overlay**) bằng C# .NET WPF.

## Tính năng chính
- **Icon Nổi Always-On-Top**: Luôn nổi trên mọi ứng dụng, hỗ trợ kéo thả vị trí mượt mà.
- **Không chiếm chỗ trên taskbar**: Cửa sổ chạy ở chế độ ToolWindow (`WS_EX_TOOLWINDOW`) nên
  không có nút taskbar và không xuất hiện trong Alt+Tab. Mọi thao tác điều khiển nằm ở
  **icon khay hệ thống** (system tray).
- **Giao diện Trong suốt**: Thiết kế không viền hiện đại, hiển thị avatar hình tròn với hiệu ứng đổ bóng.
- **Menu Bung Nút Chức Năng** (bấm vào nhân vật):
  - 📷 **Chụp màn hình**: Ẩn cửa sổ rồi mở công cụ chụp màn hình của Windows.
  - 🖥️ **Desktop**: Thu nhỏ toàn bộ cửa sổ (Win + D).
  - 💻 **Command Prompt**: Mở `cmd.exe`.
- **Tự khởi động cùng Windows**: Bật sẵn ở lần chạy đầu tiên, có thể tắt/bật lại từ menu tray.
- **Nhớ vị trí**: Vị trí kéo thả được lưu lại giữa các lần chạy.
- **Chỉ chạy một bản**: Mở lại lần hai sẽ tự thoát, không nhân đôi nhân vật.

## Icon khay hệ thống

Icon là ảnh chân dung của nhân vật, sinh từ `icon-transparent.png` (crop vùng đầu để còn
nhận ra được ở kích thước 16×16) và lưu tại `app.ico`.

> Windows 11 mặc định xếp icon mới vào **vùng ẩn**. Bấm mũi tên `^` cạnh đồng hồ để thấy,
> hoặc kéo icon ra thanh taskbar để ghim hiển thị thường trực.

**Menu chuột phải trên icon tray:**

| Mục | Tác dụng |
|---|---|
| Ẩn / Hiện nhân vật | Ẩn hoặc hiện cửa sổ nổi (nháy đúp chuột trái cũng được) |
| Đưa về giữa màn hình chính | Lối thoát khi nhân vật nằm ngoài màn hình / trên màn hình phụ đã tháo |
| Khởi động cùng Windows | Bật/tắt autostart (ghi khoá `HKCU\...\CurrentVersion\Run`, không cần quyền Admin) |
| Thoát | Đóng ứng dụng |

## Chạy khi phát triển

```bash
dotnet run
```

## Đóng gói

```powershell
.\publish.ps1            # 1 file exe self-contained (~67 MB), không cần cài .NET Runtime
.\publish.ps1 -Install   # publish rồi cài vào %LOCALAPPDATA%\Programs\IDE_Touch_Window và chạy luôn
```

| Tham số | Ý nghĩa |
|---|---|
| *(mặc định)* | Self-contained win-x64, single-file, có nén — chạy được trên máy Windows sạch |
| `-FrameworkDependent` | File nhỏ hơn nhiều nhưng máy đích phải cài **.NET 10 Desktop Runtime** |
| `-Install` | Chép exe vào `%LOCALAPPDATA%\Programs\IDE_Touch_Window` rồi khởi chạy |

Kết quả nằm ở thư mục `publish/` và chỉ gồm **đúng một file** `IDE_Touch_Window.exe`
(ảnh nhân vật và icon được nhúng vào assembly).

> Thư mục `publish/` bị xoá sạch mỗi lần chạy `publish.ps1`. Đừng đăng ký autostart trỏ
> vào đó — hãy dùng `-Install` để đưa exe về thư mục cố định.

## Gỡ cài đặt

```powershell
# 1. Thoát ứng dụng từ menu tray, hoặc:
Get-Process IDE_Touch_Window -ErrorAction SilentlyContinue | Stop-Process

# 2. Bỏ autostart
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'IDE_Touch_Window'

# 3. Xoá exe và dữ liệu (vị trí cửa sổ, cờ lần chạy đầu)
Remove-Item "$env:LOCALAPPDATA\Programs\IDE_Touch_Window" -Recurse -Force
Remove-Item "$env:LOCALAPPDATA\IDE_Touch_Window" -Recurse -Force
```

## Thay ảnh nhân vật

1. Ghi đè `icon-transparent.png` bằng ảnh PNG nền trong suốt mới.
2. Sinh lại `app.ico` (crop vùng đầu tự động) — xem `tools/make-ico.ps1`.
3. Chạy lại `.\publish.ps1`.

Ứng dụng ưu tiên đọc `icon-transparent.png` đặt cạnh file exe, nếu không có mới dùng bản
nhúng trong assembly — nên có thể đổi ảnh nhanh mà không cần build lại.
