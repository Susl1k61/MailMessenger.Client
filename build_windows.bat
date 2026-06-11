@echo off
title MailMessenger — Windows Build
setlocal

echo.
echo  =====================================================
echo   MailMessenger.Client  — Windows publish script
echo  =====================================================
echo.

:: ── Check dotnet ──────────────────────────────────────
where dotnet >nul 2>&1
if errorlevel 1 (
    echo  [ERROR] dotnet SDK not found in PATH.
    echo  Install .NET 10 SDK from https://dot.net and try again.
    pause & exit /b 1
)

:: ── Check MAUI workload ───────────────────────────────
dotnet workload list 2>nul | findstr /i "maui" >nul
if errorlevel 1 (
    echo  [INFO] Installing .NET MAUI workload...
    dotnet workload install maui-windows
)

:: ── Restore & Publish ─────────────────────────────────
echo  [1/3] Restoring packages...
dotnet restore MailMessenger.Client.csproj -r win10-x64 >nul
if errorlevel 1 goto :err

echo  [2/3] Publishing self-contained Windows app...
dotnet publish MailMessenger.Client.csproj ^
    -f net10.0-windows10.0.19041.0 ^
    -c Release ^
    -r win10-x64 ^
    --self-contained true ^
    -p:WindowsPackageType=None ^
    -p:PublishSingleFile=false ^
    -o publish\windows ^
    >build_output.log 2>&1
if errorlevel 1 goto :err

echo  [3/3] Creating launcher shortcut script...
echo @echo off > publish\windows\Launch_MailMessenger.bat
echo start "" "MailMessenger.Client.exe" >> publish\windows\Launch_MailMessenger.bat

echo.
echo  =====================================================
echo   Build complete!
echo   Output folder: publish\windows\
echo   Run:           publish\windows\MailMessenger.Client.exe
echo  =====================================================
echo.
explorer publish\windows
pause
exit /b 0

:err
echo.
echo  [ERROR] Build failed. See build_output.log for details.
type build_output.log
pause
exit /b 1
