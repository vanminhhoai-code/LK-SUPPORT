@echo off
REM Biên dịch bộ ghi Tekla cho 3 phiên bản. Sửa đường dẫn nếu Tekla cài chỗ khác.
REM Kết quả: out\21.1\MeshFrame.Tekla.dll, out\2020\MeshFrame.Tekla.dll, out\2025\MeshFrame.Tekla.dll
cd /d "%~dp0"
call :build 21.1 "C:\TeklaStructures\21.1\nt\bin\plugins"
call :build 2020 "C:\TeklaStructures\2020.0\bin"
call :build 2025 "C:\TeklaStructures\2025.0\bin"
goto :eof

:build
echo ===== Tekla %1 : %~2
if not exist "%~2\Tekla.Structures.Model.dll" (echo   BO QUA: khong thay Tekla.Structures.Model.dll & goto :eof)
dotnet build Tekla\MeshFrame.Tekla.csproj -c Release -p:TeklaBin="%~2" -o "out\%1"
goto :eof
