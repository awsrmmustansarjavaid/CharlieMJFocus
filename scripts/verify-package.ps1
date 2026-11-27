<#
.SYNOPSIS
    Checks that a packaged folder really contains the expected EXE, then records sizes and SHA-256 hashes.
.DESCRIPTION
    Fails (so the CI job turns red) if the EXE is missing or suspiciously small. Otherwise it writes
    SHA256SUMS.txt next to the files and appends a table to the GitHub Actions run summary, so a downloaded
    file can be compared with what the build actually produced. If a downloaded zip is missing the EXE but
    this summary shows it was built, the file was removed on your PC (see docs/TROUBLESHOOTING.md).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Directory,
    [Parameter(Mandatory)][string]$ExeFilter,
    [Parameter(Mandatory)][string]$Title,
    [double]$MinimumMegabytes = 20
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $Directory)) { throw "Package folder not found: $Directory" }

$exe = Get-ChildItem $Directory -Filter $ExeFilter -File | Select-Object -First 1
if (-not $exe) {
    Get-ChildItem $Directory -Recurse | Format-Table FullName, Length | Out-String | Write-Host
    throw "No file matching '$ExeFilter' in $Directory. The package is incomplete."
}

$megabytes = $exe.Length / 1MB
if ($megabytes -lt $MinimumMegabytes) {
    throw ("{0} is only {1:N1} MB; a self-contained build should be at least {2} MB." -f $exe.Name, $megabytes, $MinimumMegabytes)
}

$files = Get-ChildItem $Directory -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' }
$rows = foreach ($file in $files) {
    $hash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash
    [pscustomobject]@{ Name = $file.Name; Size = $file.Length; Hash = $hash }
}

# Same format as the standard sha256sum tool, so it can be verified anywhere.
$rows | ForEach-Object { "{0}  {1}" -f $_.Hash.ToLowerInvariant(), $_.Name } | Set-Content (Join-Path $Directory 'SHA256SUMS.txt')

$table = @("### $Title", '', '| File | Size | SHA-256 |', '| --- | ---: | --- |')
$table += $rows | ForEach-Object { '| {0} | {1:N1} MB | `{2}...` |' -f $_.Name, ($_.Size / 1MB), $_.Hash.Substring(0, 16) }
$table += ''

$table | ForEach-Object { Write-Host $_ }
if ($env:GITHUB_STEP_SUMMARY) { $table | Add-Content -Path $env:GITHUB_STEP_SUMMARY }

Write-Host ("OK: {0} is {1:N1} MB." -f $exe.Name, $megabytes)
