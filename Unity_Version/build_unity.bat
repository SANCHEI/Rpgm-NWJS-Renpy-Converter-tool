@echo off
setlocal

set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "OUTDIR=bin"
set "SOURCE=UnityExtractorForm.cs"

if not exist "%OUTDIR%" mkdir "%OUTDIR%"

echo Building Unity Asset Extractor...

%CSC% /target:winexe /out:%OUTDIR%\UnityAssetExtractor.exe /win32icon:app.ico %SOURCE% /reference:"C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.Windows.Forms.dll" /reference:"C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.Drawing.dll" /reference:"C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.dll"

if exist %OUTDIR%\UnityAssetExtractor.exe (
    echo Build complete!
    echo Output: %OUTDIR%\UnityAssetExtractor.exe
) else (
    echo Build failed!
)
