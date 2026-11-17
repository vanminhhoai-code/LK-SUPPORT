# MeshFrame - khung lưới (V + lưới + nẹp + gia cường)

Cấu trúc theo kiểu Cover Tool (Core không tham chiếu Tekla):

| Thư mục | Vai trò | Phụ thuộc |
|---|---|---|
| `Core/` | Thông số, hình học thuần, `MeshFramePlan`, `IModelWriter`, `MeshFrameBuilder.Build` | không |
| `UI/` | `MeshFrameDialog` (WinForms) + xem trước trực tiếp | WinForms |
| `Tekla/` | `TeklaModelWriter : IModelWriter` ghi vào Tekla | Tekla.Structures(.Model).dll đúng phiên bản |
| `Tests/` | test Core chạy trên Linux (`mcs` + `mono`) | không |

Gọi từ Cover Tool:

```csharp
var s = dialog.Settings;                                  // MeshFrameSettings
var r = MeshFrameBuilder.Build(s, corners /*V3[4], toạ độ GLOBAL*/, writer);
// writer = TeklaModelWriter nạp từ out\<phiên bản>\MeshFrame.Tekla.dll
```

## Chạy app (PC có Tekla)

1. `build-app.bat` -> `out\21.1`, `out\2020`, `out\2025`, mỗi thư mục có `MeshFrame.exe` (+ `MeshFrame.ini` trỏ đúng thư mục Tekla).
2. Mở Tekla + model, chạy `MeshFrame.exe` của đúng phiên bản.
3. Nhập thông số -> OK -> pick 4 điểm trong Tekla (ngược chiều kim đồng hồ nhìn từ trên) -> tạo. Thông số được nhớ ở `%APPDATA%\MeshFrame\settings.ini`.

Kiểm tra không cần Tekla (Linux/CI): `ci/build.sh` (test Core, build UI, compile app + bộ ghi với DLL Tekla 2021 và 2025 từ NuGet).
