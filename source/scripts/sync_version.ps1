param(
    [string]$Version = "",
    [switch]$Check
)

$ErrorActionPreference = "Stop"

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$versionFile = Join-Path $root "version.txt"

function Read-Version {
    $raw = [IO.File]::ReadAllText($versionFile).Trim()
    if ($raw -notmatch '^\d+\.\d+\.\d+$') {
        throw "version.txt must contain a single X.Y.Z version, got: $raw"
    }
    return $raw
}

function Get-Replacements([string]$from, [string]$to) {
    return @(
        @{ File = "build_release.ps1"; Old = '$version = "' + $from + '"'; New = '$version = "' + $to + '"' },
        @{ File = "source\Properties\AssemblyInfo.cs"; Old = 'AssemblyVersion("' + $from + '.0")'; New = 'AssemblyVersion("' + $to + '.0")' },
        @{ File = "source\Properties\AssemblyInfo.cs"; Old = 'AssemblyFileVersion("' + $from + '.0")'; New = 'AssemblyFileVersion("' + $to + '.0")' },
        @{ File = "source\Properties\AssemblyInfo.cs"; Old = 'AssemblyInformationalVersion("' + $from + '")'; New = 'AssemblyInformationalVersion("' + $to + '")' },
        @{ File = "source\Application\RpgmvpConverterForm.Ui.cs"; Old = "Game Asset Tool v$from"; New = "Game Asset Tool v$to" },
        @{ File = "source\Reporting\OperationResult.cs"; Old = '"' + $from + '",'; New = '"' + $to + '",' }
    )
}

# Detects the version currently stamped across the tree (all spots must agree).
function Get-StampedVersion {
    $text = [IO.File]::ReadAllText((Join-Path $root "build_release.ps1"))
    $match = [regex]::Match($text, '\$version = "(\d+\.\d+\.\d+)"')
    if (-not $match.Success) {
        throw "Could not find `$version in build_release.ps1"
    }
    return $match.Groups[1].Value
}

if ($Check) {
    $expected = Read-Version
    $failed = $false
    foreach ($item in (Get-Replacements $expected $expected)) {
        $path = Join-Path $root $item.File
        $text = [IO.File]::ReadAllText($path)
        if (-not $text.Contains($item.New)) {
            Write-Host ("MISSING $($item.File): $($item.New)")
            $failed = $true
        }
    }
    $changelog = [IO.File]::ReadAllText((Join-Path $root "CHANGELOG.md"))
    if (-not $changelog.Contains("## $expected")) {
        Write-Host "MISSING CHANGELOG.md: ## $expected (add release notes manually)"
        $failed = $true
    }
    if ($failed) {
        throw "Version stamp check failed for $expected."
    }
    Write-Host "Version stamp check passed for $expected."
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Usage: sync_version.ps1 <X.Y.Z> [-Check]. With no -Check, also updates version.txt."
}

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must look like X.Y.Z, got: $Version"
}

$current = Get-StampedVersion
if ($current -eq $Version) {
    Write-Host "Tree already stamped $Version."
} else {
    foreach ($item in (Get-Replacements $current $Version)) {
        $path = Join-Path $root $item.File
        # Byte-exact swap: preserves line endings and the no-trailing-newline style.
        $bytes = [IO.File]::ReadAllBytes($path)
        $text = [Text.Encoding]::UTF8.GetString($bytes)
        if (-not $text.Contains($item.Old)) {
            throw "Expected marker not found in $($item.File): $($item.Old)"
        }
        $text = $text.Replace($item.Old, $item.New)
        [IO.File]::WriteAllBytes($path, [Text.Encoding]::UTF8.GetBytes($text))
    }
    Write-Host "Stamped $Version (was $current)."
}

Set-Content -LiteralPath $versionFile -Encoding Ascii -Value $Version
Write-Host "version.txt now holds $Version. Remember to add a CHANGELOG.md entry manually."
