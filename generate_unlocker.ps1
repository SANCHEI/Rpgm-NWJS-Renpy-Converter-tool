$ErrorActionPreference = 'Stop'
$baseDir = "D:\123123\converter_WinForms"

$softFiles = @{}
Get-ChildItem "$baseDir\unlocker_soft" -Recurse -File | ForEach-Object {
    $relPath = $_.FullName.Replace("$baseDir\unlocker_soft\", "")
    $content = [Convert]::ToBase64String([IO.File]::ReadAllBytes($_.FullName))
    $softFiles[$relPath] = $content
}

$hardFiles = @{}
Get-ChildItem "$baseDir\unlocker_hard" -Recurse -File | ForEach-Object {
    $relPath = $_.FullName.Replace("$baseDir\unlocker_hard\", "")
    $content = [Convert]::ToBase64String([IO.File]::ReadAllBytes($_.FullName))
    $hardFiles[$relPath] = $content
}

$lines = @()
$lines += 'using System;'
$lines += 'using System.Collections.Generic;'
$lines += 'using System.IO;'
$lines += ''
$lines += 'namespace RpgmvpConverterWinForms'
$lines += '{'
$lines += '    internal static class UnlockerResources'
$lines += '    {'
$lines += '        private static readonly Dictionary<string, Dictionary<string, string>> Data;'
$lines += '        static UnlockerResources()'
$lines += '        {'
$lines += '            Data = new Dictionary<string, Dictionary<string, string>>();'
$lines += '            var soft = new Dictionary<string, string>();'

foreach ($kvp in $softFiles.GetEnumerator()) {
    $key = $kvp.Key
    $lines += "            soft[@`"$key`"] = `"$($kvp.Value)`";"
}

$lines += '            Data["soft"] = soft;'
$lines += '            var hard = new Dictionary<string, string>();'

foreach ($kvp in $hardFiles.GetEnumerator()) {
    $key = $kvp.Key
    $lines += "            hard[@`"$key`"] = `"$($kvp.Value)`";"
}

$lines += '            Data["hard"] = hard;'
$lines += '        }'
$lines += ''
$lines += '        public static void ExtractUnlocker(string mode, string destPath)'
$lines += '        {'
$lines += '            Dictionary<string, string> files;'
$lines += '            if (!Data.TryGetValue(mode, out files)) return;'
$lines += '            Directory.CreateDirectory(destPath);'
$lines += '            foreach (var kvp in files)'
$lines += '            {'
$lines += '                string fullPath = Path.Combine(destPath, kvp.Key);'
$lines += '                string dir = Path.GetDirectoryName(fullPath);'
$lines += '                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);'
$lines += '                File.WriteAllBytes(fullPath, Convert.FromBase64String(kvp.Value));'
$lines += '            }'
$lines += '        }'
$lines += '    }'
$lines += '}'

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllLines("$baseDir\UnlockerResources.cs", $lines, $utf8NoBom)
Write-Host "Done - $($softFiles.Count) soft files, $($hardFiles.Count) hard files"
