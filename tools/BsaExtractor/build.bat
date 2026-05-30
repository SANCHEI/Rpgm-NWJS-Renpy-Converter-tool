@echo off
set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo csc.exe not found
    exit /b 1
)

mkdir bin 2>nul

%CSC% /target:exe /out:bin\BsaExtractor.exe Program.cs

if exist bin\BsaExtractor.exe (
    echo Build complete!
) else (
    echo Build failed!
)