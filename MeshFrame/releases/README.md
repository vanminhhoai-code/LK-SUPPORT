# Các bản app đã giao (file chạy)
- `v19/` = MeshFrame v19 / 1.2.8, bản MỚI NHẤT do người dùng đưa lên (EXE + hướng dẫn + kết quả kiểm chứng + SHA256).
  Mã nguồn của 1.2.8 KHÔNG nằm trong repo này: mã nguồn trong `MeshFrame/` (Core, UI, Tekla, App, Tests) là bản 1.2.0.

## Mã nguồn v19 (1.2.8)
`MeshFrame/source-v19/MeshFrame-source-v19/` = mã nguồn của v19 do người dùng đưa lên (đã lưu nguyên vẹn), kèm `MeshFrame/source-v19/MeshFrame-validation-v19/` (chương trình kiểm chứng hồi quy + Undo và kết quả).
Cấu trúc khác cây `MeshFrame/` cũ (1.2.0): mỗi kiểu một file, `net48` + `UseWindowsForms` (chỉ build được trên Windows, không build được trên Linux CI), không có Tests/ci/SDK giả.
Cây `MeshFrame/` cũ (1.2.0) giữ nguyên để tham khảo; bản làm việc mới nhất là `source-v19`.
