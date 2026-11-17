#!/usr/bin/env bash
# Kiểm tra trên Linux/CI (không cần Tekla cài sẵn):
#  1) biên dịch + chạy test Core   2) biên dịch UI   3) biên dịch bộ ghi Tekla với DLL thật từ NuGet (2021, 2025)
# Cần: mono-mcs, mono-runtime, libmono-system-windows-forms4.0-cil, curl, unzip
set -euo pipefail
cd "$(dirname "$0")/.."
OUT="${OUT:-$(mktemp -d)}"; mkdir -p "$OUT"
echo "== Core + test"; mcs -out:"$OUT/PlanTests.exe" Core/*.cs Tests/PlanTests.cs -r:System.Core.dll; mono "$OUT/PlanTests.exe"
echo "== UI";        mcs -target:library -out:"$OUT/MeshFrame.dll" -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Core.dll Core/*.cs UI/*.cs
for v in ${TEKLA_PKG_VERSIONS:-2021.0.0 2025.0.0}; do
  echo "== Tekla writer vs Tekla.Structures.Model $v (NuGet)"
  for p in tekla.structures tekla.structures.model; do
    mkdir -p "$OUT/$v/$p"; curl -sSfL -o "$OUT/$v/$p.nupkg" "https://api.nuget.org/v3-flatcontainer/$p/$v/$p.$v.nupkg"
    (cd "$OUT/$v/$p" && unzip -q -o ../$p.nupkg)
  done
  TS=$(ls "$OUT"/$v/tekla.structures/lib/net*/Tekla.Structures.dll | grep -v netstandard | head -1)
  TM=$(ls "$OUT"/$v/tekla.structures.model/lib/net*/Tekla.Structures.Model.dll | grep -v netstandard | head -1)
  mcs -target:library -out:"$OUT/MeshFrame.Tekla.$v.dll" -r:"$TS" -r:"$TM" -r:System.Core.dll Core/*.cs Tekla/*.cs
  echo "== MeshFrame.exe (app đầy đủ) vs $v"
  mcs -target:winexe -out:"$OUT/MeshFrame.$v.exe" -r:"$TS" -r:"$TM" -r:System.Core.dll -r:System.Windows.Forms.dll -r:System.Drawing.dll Core/*.cs UI/*.cs Tekla/*.cs App/*.cs
done
echo "OK"
