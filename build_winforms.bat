@echo off
setlocal

call powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0source\scripts\build_portable_runtime.ps1"
if errorlevel 1 exit /b 1

call powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0source\scripts\build_compressed_tools.ps1"
if errorlevel 1 exit /b 1

set "MSBUILD="
if exist "%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" (
    for /f "usebackq delims=" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe"`) do set "MSBUILD=%%i"
)
if not defined MSBUILD set "MSBUILD=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"
if not exist "%MSBUILD%" (
    echo MSBuild.exe not found. Install Visual Studio Build Tools or .NET Framework.
    exit /b 1
)
echo Using MSBuild at %MSBUILD%
echo NOTE: the project targets C# 12, so the legacy .NET Framework compiler cannot build it.

"%MSBUILD%" "%~dp0GameAssetTool.csproj" /nologo /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU
if errorlevel 1 exit /b 1

copy /Y "%~dp0bin\Release\GameAssetTool.exe" "%~dp0bin\GameAssetTool.exe" >nul
if errorlevel 1 exit /b 1

echo Build complete!
echo Output: %~dp0bin\GameAssetTool.exe
