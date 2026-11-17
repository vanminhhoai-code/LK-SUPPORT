@echo off
REM Tao MeshFrame.exe (app) + MeshFrame.Tekla.dll (bo ghi) cho tung phien ban Tekla.
REM Ket qua: out\21.1\MeshFrame.exe, out\2020\MeshFrame.exe, out\2025\MeshFrame.exe
REM Sua duong dan neu Tekla cai cho khac. Phien ban nao khong co Tekla tren may se bi bo qua.
cd /d "%~dp0"
call :build 21.1 "C:\TeklaStructures\21.1\nt\bin\plugins"
call :build 2020 "C:\TeklaStructures\2020.0\bin"
call :build 2025 "C:\TeklaStructures\2025.0\bin"
goto :eof

:build
echo ===== Tekla %1 : %~2
if not exist "%~2\Tekla.Structures.Model.dll" (echo   BO QUA: khong thay Tekla.Structures.Model.dll & goto :eof)
dotnet build App\MeshFrame.App.csproj -c Release -p:TeklaBin="%~2" -o "out\%1"
dotnet build Tekla\MeshFrame.Tekla.csproj -c Release -p:TeklaBin="%~2" -o "out\%1\writer"
copy /y MeshFrame.ini.example "out\%1\MeshFrame.ini" >nul
powershell -NoProfile -Command "(Get-Content 'out\%1\MeshFrame.ini') -replace '^TeklaBin=.*','TeklaBin=%~2' | Set-Content 'out\%1\MeshFrame.ini'"
goto :eof
