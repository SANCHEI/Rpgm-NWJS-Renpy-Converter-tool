@echo off
setlocal

call powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0source\scripts\build_portable_runtime.ps1"
if errorlevel 1 exit /b 1

set "MSBUILD=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
if not exist "%MSBUILD%" (
    echo MSBuild.exe not found at %MSBUILD%
    exit /b 1
)

"%MSBUILD%" "%~dp0GameAssetTool.csproj" /nologo /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU
if errorlevel 1 exit /b 1

copy /Y "%~dp0bin\Release\GameAssetTool.exe" "%~dp0bin\GameAssetTool.exe" >nul
if errorlevel 1 exit /b 1

echo Build complete!
echo Output: %~dp0bin\GameAssetTool.exe
