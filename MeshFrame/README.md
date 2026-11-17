# MeshFrame - khung lưới (V + lưới + nẹp + gia cường)

Theo chuẩn 05.TC.01 REV04: V-25x2 (khung = gia cường, cùng tiết diện), nẹp PL-25x1.2, vít SC06@250, L ≤ 4000, H ≤ 1200, @ ≤ 1000. Thứ tự lớp: vật chủ → cánh nằm V → lưới → nẹp PL (phía ngoài) → đầu vít. V gia cường cắt khuyết hai đầu cho khớp V khung. Mọi tiết diện nhập được.

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
   Tự tìm API ở `nt\bin\plugins` (21.1, 2020) hoặc `bin` (2025); chỉ tay bằng `--tekla-bin=` hoặc `MeshFrame.ini`.
3. **Pick đa giác** trong Tekla (từ 3 điểm, lồi hoặc lõm, không tự cắt): mỗi điểm được vẽ thật kèm tên P1..Pn và chiều dài đoạn nối; thanh trạng thái hiện đoạn trước và chu vi. Kết thúc bằng **chuột giữa**; app tự nối điểm cuối về điểm đầu. Pick ngược chiều kim đồng hồ nhìn từ trên thì phía "trên" của khung là phía nhìn.
4. **Hình thật hiện trong app** (chưa tạo gì trong Tekla): chỉnh thông số, sửa tọa độ điểm, xem cảnh báo (vd khung quá nhỏ không cần/không tạo được gia cường). Nút **Pick lại điểm** để pick lại.
5. Bấm **Tạo khung trong Tekla**. Hộp thoại giữ nguyên để chỉnh tiếp.
6. **Sửa khung đã chọn**: chọn khung (một part bất kỳ của nó) trong Tekla, bấm nút -> thông số và điểm cũ hiện lại; sửa rồi bấm "Cập nhật khung" -> tạo khung mới rồi xóa khung cũ (lỗi thì giữ khung cũ). Mã khung gắn vào UDA `MESHFRAME_ID` của part chính; thông số + điểm lưu ở `<thư mục model>\MeshFrame\<mã>.mfr` (model phải đã lưu).
7. Tab **Lưới**: *RebarMesh* (mặc định, tự thay bằng tấm mỏng nếu Tekla từ chối) / *Tấm mỏng* / *Không tạo*. Mác và cỡ sợi RebarMesh được kiểm tra với catalog rebar của Tekla (có ô chọn từ catalog). Tab **Tekla**: profile ghi đè, hướng đặt thép góc/nẹp, lệch ngang, đảo cắt vát, nút *Đọc lưới mẫu* (ghi mọi thuộc tính đối tượng đang chọn ra `%APPDATA%\MeshFrame\selected-object.txt`).
Thông số nhớ ở `%APPDATA%\MeshFrame\settings.ini`. Vít: chưa có (sẽ làm thành item sau).

## Kiểm tra (không cần Tekla): `ci/build.sh`
Test Core (đa giác lồi/lõm, gia cường, nẹp, cắt khuyết...) · build app · test bộ ghi/picker/dịch vụ khung trên SDK giả · **đối chiếu từng thành viên Tekla mà bộ ghi dùng (kiểu, hàm tạo, thuộc tính, hàm, kiểu đối số, enum) với DLL thật**: NuGet 2021 và 2025 tự chạy.
Thêm bản khác: `REAL_SDK_DIRS="<thư mục có Tekla.Structures.dll + Tekla.Structures.Model.dll>" ci/build.sh` (dùng cho 21.1 và 2020 khi có DLL).

Trạng thái kiểm chứng: API tạo model đã đối chiếu **2021 và 2025**; **21.1 và 2020 chưa** (chưa có DLL). Chưa chạy trong Tekla thật.

## Nhập vào Cover Tool
`TeklaApi` cùng kiểu `Call/Get/Set` của `SdkMetadata`: tạo từ assembly gateway đã nạp, gán `api.Invoke = SdkThread.Invoke`, truyền `model` của gateway vào `TeklaModelWriter(api, model)`. Gọi `MeshFrameBuilder.Build(settings, corners /*V3[4], GLOBAL*/, writer)`.
