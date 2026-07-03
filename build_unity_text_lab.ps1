$ErrorActionPreference = "Stop"

$root = [IO.Path]::GetFullPath($PSScriptRoot)
$version = "2.4.8"
$project = [IO.Path]::GetFullPath((Join-Path $root "source\tools\UnityTextLab\UnityTextLab.csproj"))
$output = [IO.Path]::GetFullPath((Join-Path $root "source\tools\UnityTextLab\bin\Release\UnityTextLab.exe"))
$releaseTools = [IO.Path]::GetFullPath((Join-Path $root "release\tools"))
$releaseExe = [IO.Path]::GetFullPath((Join-Path $releaseTools "UnityTextLab-v$version.exe"))
$toolsZip = [IO.Path]::GetFullPath((Join-Path (Split-Path $releaseTools -Parent) "GameAssetTool-tools-experimental-v$version.zip"))
$msbuild = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe"

foreach ($path in @($project, $releaseTools, $releaseExe, $toolsZip)) {
    if (-not $path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to use path outside workspace: $path"
    }
}

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "source\scripts\build_portable_runtime.ps1")
if ($LASTEXITCODE -ne 0) { throw "Portable runtime build failed with exit code $LASTEXITCODE" }

if (-not (Test-Path -LiteralPath $msbuild)) {
    throw "MSBuild.exe not found at $msbuild"
}

& $msbuild $project /nologo /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU
if ($LASTEXITCODE -ne 0) { throw "UnityTextLab build failed with exit code $LASTEXITCODE" }

if (-not (Test-Path -LiteralPath $releaseTools)) {
    New-Item -ItemType Directory -Path $releaseTools | Out-Null
}
if (Test-Path -LiteralPath $releaseExe) {
    Remove-Item -LiteralPath $releaseExe -Force
}
Copy-Item -LiteralPath $output -Destination $releaseExe

$bytes = [IO.File]::ReadAllBytes($releaseExe)
$text = [Text.Encoding]::UTF8.GetString($bytes)
foreach ($resource in @(
    "RpgmvpConverterWinForms.scripts.unity_text_lab.py",
    "RpgmvpConverterWinForms.runtime.runtime-win-x64.zip"
)) {
    if (-not $text.Contains($resource)) {
        throw "UnityTextLab resource is missing: $resource"
    }
}

if (Test-Path -LiteralPath $toolsZip) {
    Remove-Item -LiteralPath $toolsZip -Force
}
Compress-Archive -LiteralPath $releaseExe -DestinationPath $toolsZip -Force
Write-Host "UnityTextLab ready: $releaseExe"
Write-Host "Experimental tools zip ready: $toolsZip"
