@echo off
chcp 65001 >nul

:: VISTASystem が起動中なら中断
tasklist /fi "imagename eq VISTASystem.exe" 2>nul | find /i "VISTASystem.exe" >nul
if %errorlevel% equ 0 (
    echo [エラー] VISTASystem.exe が起動中です。終了してから実行してください。
    pause
    exit /b 1
)

:: 古い publish フォルダを削除してクリーンビルド
if exist publish (
    echo 古いファイルを削除中...
    rmdir /s /q publish
)

echo ビルド中...
dotnet publish VISTASystem.csproj -p:PublishProfile=Release
if %errorlevel% neq 0 (
    echo.
    echo [エラー] ビルドに失敗しました。
    pause
    exit /b 1
)

:: pdb（デバッグシンボル）を削除
if exist publish\VISTASystem.pdb del /q publish\VISTASystem.pdb

echo.
echo 完了: publish\VISTASystem.exe
explorer publish
