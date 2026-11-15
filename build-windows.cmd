@echo off
chcp 65001>nul
setlocal
cd /d "%~dp0"

dotnet publish src\SwashbucklerDiary.Maui\SwashbucklerDiary.Maui.csproj -c Release -f net10.0-windows10.0.19041.0 --sc -p:WindowsAppSDKSelfContained=true -p:WindowsPackageType=None
if errorlevel 1 exit /b 1

set "SRC=src\SwashbucklerDiary.Maui\bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"
if exist artifacts\diary-sync-win rmdir /s /q artifacts\diary-sync-win
xcopy /e /i /q /y "%SRC%" artifacts\diary-sync-win >nul
echo 构建完成，双击 启动侠客日记.cmd 即可运行。
