@echo off
setlocal

set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "OUTDIR=bin"
set "SOURCE=UnityExtractorForm.cs"

if not exist "%OUTDIR%" mkdir "%OUTDIR%"

echo Building Game Asset Tool - Unity Edition...

set "ICON_FLAG="
if exist "app.ico" set "ICON_FLAG=/win32icon:app.ico"

%CSC% /target:winexe /out:%OUTDIR%\GameAssetTool.exe %ICON_FLAG% %SOURCE% /reference:"C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.Windows.Forms.dll" /reference:"C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.Drawing.dll" /reference:"C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.dll"

if exist %OUTDIR%\GameAssetTool.exe (
    echo Build complete!
    echo Output: %OUTDIR%\GameAssetTool.exe
) else (
    echo Build failed!
)
