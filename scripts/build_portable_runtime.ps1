param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$payloadDir = [IO.Path]::GetFullPath((Join-Path $root "payload"))
$payloadZip = [IO.Path]::GetFullPath((Join-Path $payloadDir "runtime-win-x64.zip"))
$payloadVersionFile = [IO.Path]::GetFullPath((Join-Path $payloadDir "runtime-win-x64.version.txt"))
$work = [IO.Path]::GetFullPath((Join-Path $root "obj\portable-runtime"))
$runtime = [IO.Path]::GetFullPath((Join-Path $work "runtime"))
$sitePackages = [IO.Path]::GetFullPath((Join-Path $runtime "Lib\site-packages"))
$pythonArchive = [IO.Path]::GetFullPath((Join-Path $env:TEMP "python-3.12.10-embed-amd64.zip"))
$requirements = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "portable-runtime-requirements.txt"))
$offlineOodle = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "pyuepak_oodle_offline.py"))

$runtimeVersion = "python-3.12.10-unrpa-2.3.0-unitypy-1.25.0-pyuepak-0.2.7-win-x64-v3"
$pythonUrl = "https://www.python.org/ftp/python/3.12.10/python-3.12.10-embed-amd64.zip"
$pythonSha256 = "4ACBED6DD1C744B0376E3B1CF57CE906F9DC9E95E68824584C8099A63025A3C3"

foreach ($path in @($payloadDir, $payloadZip, $payloadVersionFile, $work, $runtime, $sitePackages)) {
    if (-not $path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to use path outside workspace: $path"
    }
}

if (-not $Force -and (Test-Path -LiteralPath $payloadZip) -and (Test-Path -LiteralPath $payloadVersionFile) -and ((Get-Content -LiteralPath $payloadVersionFile -Raw).Trim() -eq $runtimeVersion)) {
    Write-Host "Portable runtime is up to date: $payloadZip"
    exit 0
}

$bootstrap = Get-Command python -ErrorAction Stop
$bootstrapInfo = & $bootstrap.Source -c "import platform, sys; print('{0}.{1}|{2}'.format(sys.version_info[0], sys.version_info[1], platform.architecture()[0]))"
if ($LASTEXITCODE -ne 0 -or $bootstrapInfo.Trim() -ne "3.12|64bit") {
    throw "Building the portable runtime requires Python 3.12 x64."
}

if ((Test-Path -LiteralPath $pythonArchive) -and ((Get-FileHash -LiteralPath $pythonArchive -Algorithm SHA256).Hash -ne $pythonSha256)) {
    Remove-Item -LiteralPath $pythonArchive -Force
}

if (-not (Test-Path -LiteralPath $pythonArchive)) {
    Write-Host "Downloading official Python embeddable runtime..."
    Invoke-WebRequest -Uri $pythonUrl -OutFile $pythonArchive
}

if ((Get-FileHash -LiteralPath $pythonArchive -Algorithm SHA256).Hash -ne $pythonSha256) {
    throw "Python embeddable runtime SHA-256 mismatch."
}

if (Test-Path -LiteralPath $work) {
    Remove-Item -LiteralPath $work -Recurse -Force
}
New-Item -ItemType Directory -Path $runtime | Out-Null
Expand-Archive -LiteralPath $pythonArchive -DestinationPath $runtime

New-Item -ItemType Directory -Path $sitePackages | Out-Null
@(
    "python312.zip"
    "."
    "Lib\site-packages"
    "import site"
) | Set-Content -LiteralPath (Join-Path $runtime "python312._pth") -Encoding Ascii

Write-Host "Installing pinned Python packages into the portable runtime..."
& $bootstrap.Source -m pip install `
    --disable-pip-version-check `
    --no-compile `
    --only-binary=:all: `
    --target $sitePackages `
    --upgrade `
    --requirement $requirements
if ($LASTEXITCODE -ne 0) {
    throw "pip install failed with exit code $LASTEXITCODE"
}

Copy-Item -LiteralPath $offlineOodle -Destination (Join-Path $sitePackages "pyuepak\oodle.py") -Force

Get-ChildItem -LiteralPath $runtime -Directory -Recurse -Force |
    Where-Object { $_.Name -eq "__pycache__" } |
    Sort-Object FullName -Descending |
    Remove-Item -Recurse -Force
Get-ChildItem -LiteralPath $runtime -File -Recurse -Force -Filter "*.pyc" |
    Remove-Item -Force

Set-Content -LiteralPath (Join-Path $runtime "GameAssetTool-runtime.txt") -Encoding Ascii -Value $runtimeVersion

Write-Host "Verifying portable runtime imports..."
& (Join-Path $runtime "python.exe") -c "import unrpa, UnityPy, pyuepak; from pyuepak.oodle import oodle; print('portable-runtime-ok')"
if ($LASTEXITCODE -ne 0) {
    throw "Portable runtime import verification failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $payloadDir)) {
    New-Item -ItemType Directory -Path $payloadDir | Out-Null
}
if (Test-Path -LiteralPath $payloadZip) {
    Remove-Item -LiteralPath $payloadZip -Force
}
Compress-Archive -Path (Join-Path $runtime "*") -DestinationPath $payloadZip -CompressionLevel Optimal
Set-Content -LiteralPath $payloadVersionFile -Encoding Ascii -Value $runtimeVersion

Write-Host "Portable runtime payload ready: $payloadZip"
