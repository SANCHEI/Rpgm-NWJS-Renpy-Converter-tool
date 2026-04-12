@echo off
setlocal

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
    echo csc.exe not found at %CSC%
    exit /b 1
)

if not exist "%~dp0bin" mkdir "%~dp0bin"

set ICON_FLAG=
if exist "%~dp0app.ico" set ICON_FLAG=/win32icon:"%~dp0app.ico"

"%CSC%" /nologo /target:winexe /optimize+ /out:"%~dp0bin\RpgmvpConverterWinForms.exe" %ICON_FLAG% /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll "%~dp0Program.cs" "%~dp0RpgmvpConverterForm.cs" "%~dp0RpaExtractor.cs" "%~dp0UnlockerResources.cs"

echo Build complete!
