$ErrorActionPreference = "Stop"

$root = [IO.Path]::GetFullPath($PSScriptRoot)
$release = [IO.Path]::GetFullPath((Join-Path $root "release"))
$releaseExe = [IO.Path]::GetFullPath((Join-Path $release "GameAssetTool-v2.2.0.exe"))
$obsoletePreviousReleaseExe = [IO.Path]::GetFullPath((Join-Path $release "GameAssetTool-v2.1.0.exe"))
$obsoletePreviousMajorExe = [IO.Path]::GetFullPath((Join-Path $release "GameAssetTool-v2.0.0.exe"))
$obsoleteCurrentMajorExe = [IO.Path]::GetFullPath((Join-Path $release "GameAssetTool-v1.9.0.exe"))
$obsoletePreviousMinorPatchExe = [IO.Path]::GetFullPath((Join-Path $release "GameAssetTool-v1.8.1.exe"))
$obsoleteCurrentPatchExe = [IO.Path]::GetFullPath((Join-Path $release "GameAssetTool-v1.8.0.exe"))
$obsoletePreviousMinorExe = [IO.Path]::GetFullPath((Join-Path $release "GameAssetTool-v1.7.0.exe"))
$obsoleteMinorExe = [IO.Path]::GetFullPath((Join-Path $release "GameAssetTool-v1.6.1.exe"))
$obsoleteEarlierMinorExe = [IO.Path]::GetFullPath((Join-Path $release "GameAssetTool-v1.6.0.exe"))
$obsoletePatchExe = [IO.Path]::GetFullPath((Join-Path $release "GameAssetTool-v1.5.2.exe"))
$obsoletePreviousPatchExe = [IO.Path]::GetFullPath((Join-Path $release "GameAssetTool-v1.5.1.exe"))
$obsoleteOlderPatchExe = [IO.Path]::GetFullPath((Join-Path $release "GameAssetTool-v1.5.0.exe"))
$obsoleteReleaseExe = [IO.Path]::GetFullPath((Join-Path $release "GameAssetTool-v1.4.0.exe"))
$obsoleteZip = [IO.Path]::GetFullPath((Join-Path $release "GameAssetTool-v1.4.0.zip"))
$obj = [IO.Path]::GetFullPath((Join-Path $root "obj"))
$releaseBin = [IO.Path]::GetFullPath((Join-Path $root "bin\Release"))

foreach ($path in @($release, $releaseExe, $obsoletePreviousReleaseExe, $obsoletePreviousMajorExe, $obsoleteCurrentMajorExe, $obsoletePreviousMinorPatchExe, $obsoleteCurrentPatchExe, $obsoletePreviousMinorExe, $obsoleteMinorExe, $obsoleteEarlierMinorExe, $obsoletePatchExe, $obsoletePreviousPatchExe, $obsoleteOlderPatchExe, $obsoleteReleaseExe, $obsoleteZip, $obj, $releaseBin)) {
    if (-not $path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to use path outside workspace: $path"
    }
}

& cmd /c (Join-Path $root "build_winforms.bat")
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $release)) {
    New-Item -ItemType Directory -Path $release | Out-Null
}

if (Test-Path -LiteralPath $releaseExe) {
    Remove-Item -LiteralPath $releaseExe -Force
}
Copy-Item -LiteralPath (Join-Path $root "bin\GameAssetTool.exe") -Destination $releaseExe

if (Test-Path -LiteralPath $obsoletePreviousReleaseExe) {
    Remove-Item -LiteralPath $obsoletePreviousReleaseExe -Force
}
if (Test-Path -LiteralPath $obsoletePreviousMajorExe) {
    Remove-Item -LiteralPath $obsoletePreviousMajorExe -Force
}
if (Test-Path -LiteralPath $obsoleteCurrentMajorExe) {
    Remove-Item -LiteralPath $obsoleteCurrentMajorExe -Force
}
if (Test-Path -LiteralPath $obsoletePreviousMinorPatchExe) {
    Remove-Item -LiteralPath $obsoletePreviousMinorPatchExe -Force
}
if (Test-Path -LiteralPath $obsoleteCurrentPatchExe) {
    Remove-Item -LiteralPath $obsoleteCurrentPatchExe -Force
}
if (Test-Path -LiteralPath $obsoletePreviousMinorExe) {
    Remove-Item -LiteralPath $obsoletePreviousMinorExe -Force
}
if (Test-Path -LiteralPath $obsoleteMinorExe) {
    Remove-Item -LiteralPath $obsoleteMinorExe -Force
}
if (Test-Path -LiteralPath $obsoleteEarlierMinorExe) {
    Remove-Item -LiteralPath $obsoleteEarlierMinorExe -Force
}
if (Test-Path -LiteralPath $obsoleteReleaseExe) {
    Remove-Item -LiteralPath $obsoleteReleaseExe -Force
}
if (Test-Path -LiteralPath $obsoletePatchExe) {
    Remove-Item -LiteralPath $obsoletePatchExe -Force
}
if (Test-Path -LiteralPath $obsoletePreviousPatchExe) {
    Remove-Item -LiteralPath $obsoletePreviousPatchExe -Force
}
if (Test-Path -LiteralPath $obsoleteOlderPatchExe) {
    Remove-Item -LiteralPath $obsoleteOlderPatchExe -Force
}
if (Test-Path -LiteralPath $obsoleteZip) {
    Remove-Item -LiteralPath $obsoleteZip -Force
}

foreach ($path in @($obj, $releaseBin)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}

Write-Host "Single-file release ready: $releaseExe"
