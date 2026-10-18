param()
$ErrorActionPreference = "Stop"

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))

function Compress-Tool([string]$relativePath) {
    $source = [IO.Path]::GetFullPath((Join-Path $root $relativePath))
    $destination = $source + ".deflated"
    foreach ($path in @($source, $destination)) {
        if (-not $path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to use path outside workspace: $path"
        }
    }
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Tool binary was not found: $source"
    }
    if ((Test-Path -LiteralPath $destination) -and ((Get-Item -LiteralPath $destination).LastWriteTimeUtc -ge (Get-Item -LiteralPath $source).LastWriteTimeUtc)) {
        Write-Host ("Up to date: " + $destination)
        return
    }
    # Raw deflate (no header) with an 8-byte magic prefix: deterministic for
    # identical input, decompressed by DeflateStream on the C# side.
    # Must stay in sync with ToolRuntime.ExtractResource.
    $magic = [Text.Encoding]::ASCII.GetBytes("GATDEF01")
    $input = [IO.File]::OpenRead($source)
    try {
        $output = [IO.File]::Create($destination)
        try {
            $output.Write($magic, 0, $magic.Length)
            $deflate = New-Object IO.Compression.DeflateStream($output, [IO.Compression.CompressionLevel]::Optimal)
            try {
                $input.CopyTo($deflate)
            }
            finally {
                $deflate.Dispose()
            }
        }
        finally {
            $output.Dispose()
        }
    }
    finally {
        $input.Dispose()
    }
    Write-Host ("Compressed: " + $destination)
}

Compress-Tool "third_party\uberwolf\UberWolfCli.exe"
Compress-Tool "third_party\resvg\resvg.exe"
Write-Host "Compressed tools ready."
