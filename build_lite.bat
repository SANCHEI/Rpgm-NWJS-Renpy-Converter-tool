@echo off
setlocal
REM Lite build: skips the portable Python runtime so the exe stays small (~5 MB).
REM Python-backed engines (Renpy/Unity/Godot/Kirikiri/Unreal/GameMaker/SpakDat)
REM download the runtime once on first use from GitHub Releases.
REM Native extractors (RPG Maker MV/MZ, RGSS, NWJS, Electron, HTML, ...) work offline.
REM Requires: payload\runtime-win-x64.zip must NOT exist (csproj embeds it only if present).

if exist "%~dp0payload\runtime-win-x64.zip" (
    echo Lite build refused: payload\runtime-win-x64.zip exists.
    echo Move it away first if you really want a Lite exe without embedded runtime.
    exit /b 1
)

set "MSBUILD=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
if not exist "%MSBUILD%" (
    echo MSBuild.exe not found at %MSBUILD%
    exit /b 1
)

"%MSBUILD%" "%~dp0GameAssetTool.csproj" /nologo /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU
if errorlevel 1 exit /b 1

copy /Y "%~dp0bin\Release\GameAssetTool.exe" "%~dp0bin\GameAssetTool-Lite.exe" >nul
if errorlevel 1 exit /b 1

echo Lite build complete!
echo Output: %~dp0bin\GameAssetTool-Lite.exe
