#!/usr/bin/env bash
# Kiểm tra trên Linux/CI (không cần Tekla cài sẵn):
#  1) test Core   2) build UI   3) build app (reflection, không tham chiếu Tekla)
#  4) test bộ ghi trên SDK giả   5) ĐỐI CHIẾU mọi thành viên Tekla bộ ghi dùng với DLL thật (NuGet 2021, 2025; thêm bản khác qua REAL_SDK_DIRS)
# Cần: mono-mcs, mono-runtime, libmono-system-windows-forms4.0-cil, curl, unzip
set -euo pipefail
cd "$(dirname "$0")/.."
OUT="${OUT:-$(mktemp -d)}"; mkdir -p "$OUT"
echo "== Core + test"; mcs -out:"$OUT/PlanTests.exe" Core/*.cs Tests/PlanTests.cs -r:System.Core.dll; mono "$OUT/PlanTests.exe"
echo "== UI + App";    mcs -target:winexe -out:"$OUT/MeshFrame.exe" -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Core.dll Core/*.cs UI/*.cs Tekla/*.cs App/*.cs

echo "== Bộ ghi trên SDK giả"
FAKE="$OUT/fake"; mkdir -p "$FAKE"
mcs -target:library -out:"$FAKE/Tekla.Structures.dll" Tests/FakeSdk/FakeGeo.cs
mcs -target:library -out:"$FAKE/Tekla.Structures.Model.dll" -r:"$FAKE/Tekla.Structures.dll" Tests/FakeSdk/FakeModel.cs
mcs -out:"$FAKE/WriterTests.exe" -r:"$FAKE/Tekla.Structures.Model.dll" -r:System.Core.dll Core/*.cs Tekla/TeklaApi.cs Tekla/TeklaModelWriter.cs Tests/WriterTests.cs
mono "$FAKE/WriterTests.exe" "$FAKE" "$OUT/members.txt"

echo "== Đối chiếu hợp đồng với DLL Tekla thật"
mcs -out:"$OUT/ContractCheck.exe" Tests/ContractCheck.cs
DIRS="${REAL_SDK_DIRS:-}"
for v in ${TEKLA_PKG_VERSIONS:-2021.0.0 2025.0.0}; do
  d="$OUT/sdk-$v"; mkdir -p "$d"
  for p in tekla.structures tekla.structures.model; do
    curl -sSfL -o "$OUT/$p.$v.nupkg" "https://api.nuget.org/v3-flatcontainer/$p/$v/$p.$v.nupkg"
    unzip -q -o -j "$OUT/$p.$v.nupkg" "lib/net4*/*.dll" -d "$d" 2>/dev/null || unzip -q -o -j "$OUT/$p.$v.nupkg" "lib/net*/*.dll" -d "$d"
  done
  DIRS="$DIRS $d"
done
for d in $DIRS; do mono "$OUT/ContractCheck.exe" "$OUT/members.txt" "$d"; done
echo "OK"
