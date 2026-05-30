@echo off
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set OUT=bin\UnityGalleryUnlocker.exe

echo Building UnityGalleryUnlocker...

if not exist "bin" mkdir bin

"%CSC%" /target:winexe /out:%OUT% /reference:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll" /reference:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll" /reference:"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll" MainForm.cs

if errorlevel 1 (
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo Build complete!
echo Output: %OUT%
pause