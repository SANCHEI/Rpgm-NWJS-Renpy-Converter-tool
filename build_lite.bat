@echo off
setlocal
REM Lite build: portable Python runtime is not embedded (EmbedRuntime=false),
REM so the exe stays small (~5 MB). Compressed third-party tools are still
REM embedded when their .deflated files exist (see build_compressed_tools.ps1).
REM Python-backed engines (Renpy/Unity/Godot/Unreal/GameMaker/SPAK DAT)
REM download versioned runtime packs once on first use from GitHub Releases.
REM Native extractors (RPG Maker MV/MZ, RGSS, KiriKiri XP3, NWJS, Electron,
REM HTML, ...) work fully offline.

call powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0source\scripts\build_compressed_tools.ps1"
if errorlevel 1 exit /b 1

set "MSBUILD=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
if not exist "%MSBUILD%" (
    echo MSBuild.exe not found at %MSBUILD%
    exit /b 1
)

"%MSBUILD%" "%~dp0GameAssetTool.csproj" /nologo /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU /p:EmbedRuntime=false
if errorlevel 1 exit /b 1

copy /Y "%~dp0bin\Release\GameAssetTool.exe" "%~dp0bin\GameAssetTool-Lite.exe" >nul
if errorlevel 1 exit /b 1

echo Lite build complete!
echo Output: %~dp0bin\GameAssetTool-Lite.exe
