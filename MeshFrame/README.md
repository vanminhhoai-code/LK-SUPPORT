# MeshFrame - khung lưới (V + lưới + nẹp + gia cường)

Một `MeshFrame.exe` cho **Tekla 21.1 / 2020 / 2025**: API Tekla nạp lúc chạy bằng reflection (cùng nguyên tắc Cover Tool), không tham chiếu cứng DLL Tekla.

| Thư mục | Vai trò | Phụ thuộc |
|---|---|---|
| `Core/` | Thông số, hình học thuần, `MeshFramePlan`, `IModelWriter`, `MeshFrameBuilder.Build`, `SettingsStore` | không |
| `UI/` | `MeshFrameDialog` (WinForms) + xem trước trực tiếp | WinForms |
| `Tekla/` | `TeklaApi` (adapter reflection) + `TeklaModelWriter` | không (nạp DLL Tekla lúc chạy) |
| `App/` | `MeshFrame.exe`: chọn bản Tekla, kết nối, pick 4 điểm, tạo | — |
| `Tests/` | test Core, SDK giả + test bộ ghi, đối chiếu hợp đồng với DLL Tekla thật | — |

## Chạy (PC có Tekla)
1. `build-app.bat` -> `out\MeshFrame.exe` + `Start-Tekla21.1.bat` / `Start-Tekla2020.bat` / `Start-Tekla2025.bat`.
2. Mở Tekla + model, chạy file `Start-Tekla<bản>.bat` tương ứng (hoặc `MeshFrame.exe --tekla-version=2025`).
   Không chỉ bản thì thử lần lượt 2025, 2020, 21.1. Tự tìm API ở `nt\bin\plugins` (21.1, 2020) hoặc `bin` (2025); chỉ tay bằng `--tekla-bin=` hoặc `MeshFrame.ini`.
3. Nhập thông số -> OK -> pick 4 điểm trong Tekla (ngược chiều kim đồng hồ nhìn từ trên). Thông số nhớ ở `%APPDATA%\MeshFrame\settings.ini`.

## Kiểm tra (không cần Tekla): `ci/build.sh`
Test Core (33) · build app · test bộ ghi trên SDK giả · **đối chiếu từng thành viên Tekla mà bộ ghi dùng (kiểu, hàm tạo, thuộc tính, hàm, kiểu đối số, enum) với DLL thật**: NuGet 2021 và 2025 tự chạy.
Thêm bản khác: `REAL_SDK_DIRS="<thư mục có Tekla.Structures.dll + Tekla.Structures.Model.dll>" ci/build.sh` (dùng cho 21.1 và 2020 khi có DLL).

Trạng thái kiểm chứng: API tạo model đã đối chiếu **2021 và 2025**; **21.1 và 2020 chưa** (chưa có DLL). Chưa chạy trong Tekla thật.

## Nhập vào Cover Tool
`TeklaApi` cùng kiểu `Call/Get/Set` của `SdkMetadata`: tạo từ assembly gateway đã nạp, gán `api.Invoke = SdkThread.Invoke`, truyền `model` của gateway vào `TeklaModelWriter(api, model)`. Gọi `MeshFrameBuilder.Build(settings, corners /*V3[4], GLOBAL*/, writer)`.
