param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$payloadDir = [IO.Path]::GetFullPath((Join-Path $root "payload"))
$payloadZip = [IO.Path]::GetFullPath((Join-Path $payloadDir "runtime-win-x64.zip"))
$payloadVersionFile = [IO.Path]::GetFullPath((Join-Path $payloadDir "runtime-win-x64.version.txt"))
$work = [IO.Path]::GetFullPath((Join-Path $root "obj\portable-runtime"))
$runtime = [IO.Path]::GetFullPath((Join-Path $work "runtime"))
$sitePackages = [IO.Path]::GetFullPath((Join-Path $runtime "Lib\site-packages"))
$pythonArchive = [IO.Path]::GetFullPath((Join-Path $env:TEMP "python-3.12.10-embed-amd64.zip"))
$requirements = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "portable-runtime-requirements.txt"))
$localOodle = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "pyuepak_oodle_local.py"))
$windowsAes = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "pyuepak_aes_windows.py"))
$patchPyuepak = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "patch_pyuepak_offline.py"))

$runtimeVersion = "python-3.12.10-unrpa-2.3.0-unitypy-1.25.0-pyuepak-0.2.7-zstandard-0.25.0-pycryptodome-3.23.0-win-x64-v9"
$pythonUrl = "https://www.python.org/ftp/python/3.12.10/python-3.12.10-embed-amd64.zip"
$pythonSha256 = "4ACBED6DD1C744B0376E3B1CF57CE906F9DC9E95E68824584C8099A63025A3C3"

function Get-Sha256 {
    param([string]$Path)

    $stream = [IO.File]::OpenRead($Path)
    try {
        $sha = [Security.Cryptography.SHA256]::Create()
        try {
            return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace("-", "")
        }
        finally {
            $sha.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

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

if ((Test-Path -LiteralPath $pythonArchive) -and ((Get-Sha256 $pythonArchive) -ne $pythonSha256)) {
    Remove-Item -LiteralPath $pythonArchive -Force
}

if (-not (Test-Path -LiteralPath $pythonArchive)) {
    Write-Host "Downloading official Python embeddable runtime..."
    Invoke-WebRequest -Uri $pythonUrl -OutFile $pythonArchive
}

if ((Get-Sha256 $pythonArchive) -ne $pythonSha256) {
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

Copy-Item -LiteralPath $localOodle -Destination (Join-Path $sitePackages "pyuepak\oodle.py") -Force
Copy-Item -LiteralPath $windowsAes -Destination (Join-Path $sitePackages "pyuepak\aes_windows.py") -Force
& $bootstrap.Source $patchPyuepak $sitePackages
if ($LASTEXITCODE -ne 0) {
    throw "pyuepak offline patch failed with exit code $LASTEXITCODE"
}

function Remove-RuntimeItem {
    param([string]$Path)

    $fullPath = [IO.Path]::GetFullPath($Path)
    $runtimePrefix = $runtime.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($runtimePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove path outside portable runtime: $fullPath"
    }
    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
}

Write-Host "Removing files that are not needed by the embedded extractors..."
Get-ChildItem -LiteralPath $sitePackages -Directory -Recurse -Force |
    Where-Object { $_.Name -eq "__pycache__" -or $_.Name.EndsWith(".dist-info", [StringComparison]::OrdinalIgnoreCase) } |
    Sort-Object FullName -Descending |
    ForEach-Object { Remove-RuntimeItem $_.FullName }
Get-ChildItem -LiteralPath $sitePackages -File -Recurse -Force |
    Where-Object { $_.Extension -in @(".pyc", ".pyo", ".ipdb", ".iobj") } |
    ForEach-Object { Remove-RuntimeItem $_.FullName }

foreach ($optionalFile in @(
    "PIL\_avif.cp312-win_amd64.pyd",
    "PIL\_imagingcms.cp312-win_amd64.pyd",
    "PIL\_imagingft.cp312-win_amd64.pyd",
    "PIL\_webp.cp312-win_amd64.pyd"
)) {
    Remove-RuntimeItem (Join-Path $sitePackages $optionalFile)
}
Remove-RuntimeItem (Join-Path $runtime "sqlite3.dll")
Remove-RuntimeItem (Join-Path $runtime "_sqlite3.pyd")
Remove-RuntimeItem (Join-Path $runtime "python.cat")
Remove-RuntimeItem (Join-Path $runtime "libcrypto-3.dll")
Remove-RuntimeItem (Join-Path $runtime "libssl-3.dll")
Remove-RuntimeItem (Join-Path $runtime "_hashlib.pyd")
Remove-RuntimeItem (Join-Path $runtime "_ssl.pyd")
Remove-RuntimeItem (Join-Path $runtime "pythonw.exe")
Remove-RuntimeItem (Join-Path $sitePackages "bin")

Get-ChildItem -LiteralPath $runtime -Directory -Recurse -Force |
    Where-Object { $_.Name -eq "__pycache__" } |
    Sort-Object FullName -Descending |
    Remove-Item -Recurse -Force
Get-ChildItem -LiteralPath $runtime -File -Recurse -Force -Filter "*.pyc" |
    Remove-Item -Force

Set-Content -LiteralPath (Join-Path $runtime "GameAssetTool-runtime.txt") -Encoding Ascii -Value $runtimeVersion

Write-Host "Verifying portable runtime imports..."
& (Join-Path $runtime "python.exe") -c "import brotli, hashlib, os, tempfile, unrpa, UnityPy, pyuepak, zstandard; from Crypto.Cipher import ChaCha20; from PIL import Image; from pyuepak.aes_windows import aes_cfb_decrypt, aes_cfb_encrypt, aes_ecb_decrypt; from pyuepak.oodle import oodle; assert hashlib.md5(b'x').hexdigest() == '9dd4e461268c8034f5c8564e155c67a6'; assert hashlib.sha1(b'x').hexdigest() == '11f6ad8ec52a2984abaafd7c3b516503785c2072'; assert aes_ecb_decrypt(bytes.fromhex('000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f'), bytes.fromhex('8ea2b7ca516745bfeafc49904b496089')).hex() == '00112233445566778899aabbccddeeff'; assert ChaCha20.new(key=bytes(32), nonce=bytes(12)).decrypt(bytes(4)) == bytes.fromhex('76b8e0ad'); key=bytes.fromhex('603deb1015ca71be2b73aef0857d77811f352c073b6108d72d9810a30914dff4'); iv=bytes.fromhex('000102030405060708090a0b0c0d0e0f'); encrypted=bytes.fromhex('dc7e84bfda79164b7ecd8486985d3860'); plain=bytes.fromhex('6bc1bee22e409f96e93d7e117393172a'); assert aes_cfb_decrypt(key, iv, encrypted) == plain; assert aes_cfb_encrypt(key, iv, plain) == encrypted; path=os.path.join(tempfile.gettempdir(), 'GameAssetTool-pillow-test.png'); Image.new('RGBA', (1, 1)).save(path); os.remove(path); print('portable-runtime-ok')"
if ($LASTEXITCODE -ne 0) {
    throw "Portable runtime import verification failed with exit code $LASTEXITCODE"
}

Get-ChildItem -LiteralPath $runtime -Directory -Recurse -Force |
    Where-Object { $_.Name -eq "__pycache__" } |
    Sort-Object FullName -Descending |
    Remove-Item -Recurse -Force
Get-ChildItem -LiteralPath $runtime -File -Recurse -Force -Filter "*.pyc" |
    Remove-Item -Force

if (-not (Test-Path -LiteralPath $payloadDir)) {
    New-Item -ItemType Directory -Path $payloadDir | Out-Null
}
if (Test-Path -LiteralPath $payloadZip) {
    Remove-Item -LiteralPath $payloadZip -Force
}
Compress-Archive -Path (Join-Path $runtime "*") -DestinationPath $payloadZip -CompressionLevel Optimal
Set-Content -LiteralPath $payloadVersionFile -Encoding Ascii -Value $runtimeVersion

Write-Host "Portable runtime payload ready: $payloadZip"
