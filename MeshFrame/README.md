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
Khi khởi động, app **tự kiểm tra bản Tekla đang dùng có đủ hàm API mà MeshFrame cần** (21.1 / 2020 có thể thiếu so với 2021 / 2025); chỉ hỏi người dùng khi thiếu hàm **bắt buộc**; hàm tùy chọn (như `PickPoint(chuỗi, điểm)` trên 21.1, `GraphicsDrawer`) đã có dự phòng nên không làm phiền. Chi tiết ghi ở `%APPDATA%\MeshFrame\api-missing.txt` (nút *Kiểm tra API* ở tab Tekla).
**Tự canh V và nẹp:** thay vì đoán quy ước On plane / Rotation / At depth của từng bản Tekla, app tạo thanh mẫu, ĐO hình khối thật (`Part.GetSolid`: kích thước, thanh cánh nằm/cánh đứng ở phía nào qua `Solid.Intersect`), chọn hướng xoay đúng rồi canh để nẹp nằm giữa cánh nằm V và ép ngoài lưới. Không đo được thì dùng thông số và báo rõ.
Thông số nhớ ở `%APPDATA%\MeshFrame\settings.ini` (file từ bản cũ bị bỏ và sao lưu `.bak` để mặc định mới có hiệu lực). Vít: chưa có (sẽ làm thành item sau).

## Kiểm tra (không cần Tekla): `ci/build.sh`
Test Core (đa giác lồi/lõm, gia cường, nẹp, cắt khuyết...) · build app · test bộ ghi/picker/dịch vụ khung trên SDK giả · **đối chiếu từng thành viên Tekla mà bộ ghi dùng (kiểu, hàm tạo, thuộc tính, hàm, kiểu đối số, enum) với DLL thật**: NuGet 2021 và 2025 tự chạy.
Thêm bản khác: `REAL_SDK_DIRS="<thư mục có Tekla.Structures.dll + Tekla.Structures.Model.dll>" ci/build.sh` (dùng cho 21.1 và 2020 khi có DLL).

Trạng thái kiểm chứng: API tạo model đã đối chiếu **2021 và 2025**; **21.1 và 2020 chưa** (chưa có DLL). Chưa chạy trong Tekla thật.

## Nhập vào Cover Tool
`TeklaApi` cùng kiểu `Call/Get/Set` của `SdkMetadata`: tạo từ assembly gateway đã nạp, gán `api.Invoke = SdkThread.Invoke`, truyền `model` của gateway vào `TeklaModelWriter(api, model)`. Gọi `MeshFrameBuilder.Build(settings, corners /*V3[4], GLOBAL*/, writer)`.

## Bản 1.1.0
- Giao diện tối kiểu Cover Tool (UI/UiTheme.cs là bản sao nguyên vẹn của Cover Tool 1.7.128, không sửa; UI/CoverUiHost.cs là phần chủ tối thiểu; font Inter ở Fonts/). Thanh đầu trang: số phiên bản, bản Tekla, model, trạng thái kết nối.
- Xem trước 3D thật (UI/Preview3D.cs + Core/Model3D.cs): đúng tiết diện chữ L, cắt vát, khuyết, nẹp, lưới. Cuộn = zoom; giữ chuột giữa kéo = kéo hình; Ctrl + chuột giữa = xoay; nút 3D / Trên / Dưới / Vừa khung; hộp "Mặt bằng 2D" để xem số cạnh.
- Tab Nẹp: vị trí nẹp so với V (Giữa / Trái / Phải theo bề rộng cánh nằm).
- Tab Khung V: kiểu nối góc "Cắt vát 45°" hoặc "Một cạnh chạy dài, một cạnh dừng" (cạnh song song cạnh gốc chạy dài, cạnh còn lại dừng sát mặt trong + khuyết cánh nằm), dùng cho đa giác bất kỳ.
- Tổng ô nhập tay lớn hơn kích thước khung: chỉ cảnh báo, cắt ở mép khung, không khóa nút Tạo khung.
- Vòng kín trong Tekla: sau khi tạo, đo lại hình khối thật của TỪNG thanh V và nẹp (Solid.Intersect), dời thanh lệch về đúng vị trí kế hoạch, ghi số đo trong kết quả. CHƯA chạy thử trên Tekla thật.

## Bản 1.2.0
- Chuỗi kích thước đỏ gia cường (mép ngoài khung → tim V → ... → mép ngoài) đo tới TIM V gia cường (giữa bề rộng cánh nằm), vẽ trên hình 2D và 3D. Thanh V gia cường được đặt sao cho tim nằm đúng vị trí (đường tham chiếu lùi legA/2 so với bản 1.1.0).
- Hình 3D hiện số: P#, độ dài cạnh (cạnh gốc màu cam), chuỗi ô. Nút "Số: bật/tắt".
- Sửa số trực tiếp trên hình (2D và 3D): bấm đúp số cạnh / số ô để gõ giá trị mới (hoặc chọn rồi F2 / Enter). Bấm nhãn P# để chọn ĐIỂM GỐC: điểm gốc đứng yên, các điểm phía xa dời theo (kéo giãn dọc cạnh, giữ góc); điểm gốc là gốc tọa độ của mặt phẳng khung khi dựng vào Tekla. Hoàn tác bằng nút "Hoàn tác" hoặc Ctrl+Z.
- Vị trí nẹp (Giữa / Trái / Phải) chỉ áp cho nẹp trên V gia cường; nẹp quanh khung luôn trùng mép ngoài V khung.
- Cập nhật khung đã chọn là CẬP NHẬT TẠI CHỖ: sửa chính các part cũ (giữ GUID, số hiệu) theo khóa UDA MESHFRAME_KEY (B{i} / S{i} / M), thêm / xóa phần chênh lệch, lưới tạo lại; lỗi thì dựng lại khung mới và xóa khung cũ, không để hai khung đè nhau. Khung tạo từ bản cũ chưa có khóa: part chính được giữ, các part còn lại được tạo lại.
