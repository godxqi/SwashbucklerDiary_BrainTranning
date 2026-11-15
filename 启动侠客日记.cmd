@echo off
chcp 65001>nul
set "EXE=%~dp0artifacts\diary-sync-win\SwashbucklerDiary.Maui.exe"
if not exist "%EXE%" (
  echo 找不到 Windows 构建产物，请先双击 build-windows.cmd 构建。
  pause
  exit /b 1
)
start "" "%EXE%"
