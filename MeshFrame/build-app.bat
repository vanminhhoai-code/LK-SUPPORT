@echo off
REM Tao MeshFrame.exe (MOT ban cho 21.1 / 2020 / 2025). Ket qua: out\MeshFrame.exe
cd /d "%~dp0"
dotnet build App\MeshFrame.App.csproj -c Release -o out
copy /y MeshFrame.ini.example out\MeshFrame.ini >nul
copy /y Start-Tekla21.1.bat out\ >nul
copy /y Start-Tekla2020.bat out\ >nul
copy /y Start-Tekla2025.bat out\ >nul
