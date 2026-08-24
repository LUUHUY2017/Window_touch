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
.\publish.ps1            # mặc định: 1 file exe, máy đích KHÔNG cần cài gì
.\publish.ps1 -Install   # publish rồi cài vào %LOCALAPPDATA%\Programs\IDE_Touch_Window và chạy luôn
```

| Chế độ | Kết quả | Máy đích cần cài gì? |
|---|---|---|
| **Mặc định** (self-contained) | **1 file** `IDE_Touch_Window.exe` — 67.1 MB | **Không cần gì cả.** Toàn bộ .NET runtime nằm trong exe |
| `-FrameworkDependent` | 4 file — tổng ~1.2 MB | Phải cài **.NET 10 Desktop Runtime** |

Chọn cái nào?

- **Đưa cho người khác / máy lạ** → dùng mặc định. Đổi 67 MB lấy việc không phải cài đặt gì.
- **Chỉ dùng trên máy đã có .NET 10 SDK/Runtime** → `-FrameworkDependent` nhẹ hơn ~56 lần.

> `-FrameworkDependent` xuất ra **4 file**, phải chép cả thư mục chứ không chép mỗi file `.exe`.
> Bản này cố tình **không** bật `PublishSingleFile`: khi kết hợp `-r win-x64` +
> `PublishSingleFile` + `--self-contained false`, .NET SDK vẫn copy toàn bộ 265 file runtime
> vào build rồi gói hết vào exe, cho ra file 140 MB mà **vẫn** đòi máy đích cài runtime —
> tệ hơn cả hai lựa chọn trên.

Kết quả nằm ở thư mục `publish/`.

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
