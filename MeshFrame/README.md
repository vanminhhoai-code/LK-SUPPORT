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

`build-writer.bat` biên dịch `MeshFrame.Tekla.dll` cho 21.1 / 2020 / 2025 (cùng một mã nguồn).
