#!/usr/bin/env bash
# Sinh Tekla/ApiMembers.cs từ danh sách thành viên Tekla ghi lại khi chạy test bộ ghi (cho TeklaContract.VerifyLoaded trong app).
# Dùng: ci/gen-members.sh <members.txt>
set -euo pipefail
cd "$(dirname "$0")/.."
{
  echo "// TỰ SINH bởi ci/gen-members.sh từ test bộ ghi - KHÔNG sửa tay. CI kiểm tra file này khớp với danh sách thật."
  echo "namespace MeshFrame.Tekla"
  echo "{"
  echo "    internal static class ApiMembers"
  echo "    {"
  echo -n '        public const string Text = @"'
  sort -u "$1" | sed 's/"/""/g' | awk 'BEGIN{first=1} {if(!first) printf "\n"; printf "%s", $0; first=0} END{printf "\n"}'
  echo '";'
  echo "    }"
  echo "}"
} > Tekla/ApiMembers.cs
