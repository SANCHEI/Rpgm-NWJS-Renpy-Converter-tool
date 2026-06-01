$ErrorActionPreference = "Stop"

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\..\.."))
$cargo = [IO.Path]::GetFullPath((Join-Path $env:USERPROFILE ".cargo\bin\cargo.exe"))
$target = [IO.Path]::GetFullPath((Join-Path $root "obj\oozextract-target"))
$outputDir = [IO.Path]::GetFullPath((Join-Path $root "third_party\oozextract"))
$outputDll = [IO.Path]::GetFullPath((Join-Path $outputDir "GameAssetTool.OozExtract.dll"))
$builtDll = [IO.Path]::GetFullPath((Join-Path $target "release\game_asset_tool_oozextract.dll"))

foreach ($path in @($target, $outputDir, $outputDll, $builtDll)) {
    if (-not $path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to use path outside workspace: $path"
    }
}

if (-not (Test-Path -LiteralPath $cargo)) {
    throw "Rust Cargo was not found at $cargo"
}

& $cargo build --release --locked --target-dir $target --manifest-path (Join-Path $PSScriptRoot "Cargo.toml")
if ($LASTEXITCODE -ne 0) {
    throw "oozextract helper build failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}
Copy-Item -LiteralPath $builtDll -Destination $outputDll -Force
Write-Host "Oodle fallback helper ready: $outputDll"
