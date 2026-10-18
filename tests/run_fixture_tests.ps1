param(
    [string]$WorkDir = ""
)

$ErrorActionPreference = "Stop"

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
if ([string]::IsNullOrWhiteSpace($WorkDir)) {
    $WorkDir = Join-Path ([IO.Path]::GetTempPath()) "GameAssetTool-fixtures"
}
$fixtures = Join-Path $WorkDir "game"
$outRoot = Join-Path $WorkDir "out"
$harness = Join-Path $WorkDir "FixtureTest.exe"

foreach ($path in @($fixtures, $outRoot)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
    New-Item -ItemType Directory -Path $path | Out-Null
}

$python = Get-Command python -ErrorAction Stop
Write-Host "Building fixtures..."
& $python.Source "$root\tests\fixtures\mk_xp3.py" (Join-Path $fixtures "xp3") | Out-Null
& $python.Source "$root\tests\fixtures\mk_gm.py" (Join-Path $fixtures "gm") | Out-Null
& $python.Source "$root\tests\fixtures\mk_rpa.py" (Join-Path $fixtures "rpa") | Out-Null
& $python.Source "$root\tests\fixtures\mk_rpgm.py" (Join-Path $fixtures "rpgm") | Out-Null
& $python.Source "$root\tests\fixtures\mk_godot.py" (Join-Path $fixtures "godot") | Out-Null

Write-Host "Compiling fixture harness..."
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path -LiteralPath $csc)) {
    throw "C# compiler was not found: $csc"
}
$sources = @(
    "$root\tests\harness\FixtureTest.cs",
    "$root\source\Extractors\KirikiriXp3Extractor.cs",
    "$root\source\Extractors\GameMakerExtractor.cs",
    "$root\source\Extractors\RpaExtractor.cs",
    "$root\source\Extractors\PickleReader.cs",
    "$root\source\Extractors\CompressionHelper.cs",
    "$root\source\Extractors\PngEncoder.cs",
    "$root\source\Extractors\ExtractionPathUtils.cs",
    "$root\source\Extractors\RpgmCrypt.cs",
    "$root\source\Extractors\GodotExtractor.cs"
)
& $csc /nologo /out:$harness $sources /r:System.dll /r:System.Core.dll
if ($LASTEXITCODE -ne 0) {
    throw "Fixture harness compilation failed."
}

$failed = $false
foreach ($mode in @("xp3", "gm", "rpa", "rpgm", "godot")) {
    $outDir = Join-Path $outRoot $mode
    New-Item -ItemType Directory -Path $outDir | Out-Null
    $actual = & $harness $mode (Join-Path $fixtures $mode) $outDir | Out-String
    $expectedPath = Join-Path $root ("tests\expected\" + $mode + ".txt")
    $expected = [IO.File]::ReadAllText($expectedPath).Trim() -replace "`r`n", "`n"
    $normalized = $actual.Trim() -replace "`r`n", "`n"
    if ($normalized -eq $expected) {
        Write-Host "PASS $mode"
    } else {
        Write-Host "FAIL $mode"
        $failed = $true
    }
}

if ($failed) {
    throw "Fixture tests failed."
}
Write-Host "All fixture tests passed."
