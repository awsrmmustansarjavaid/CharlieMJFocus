<#
.SYNOPSIS
    Deletes all build output (bin, obj and artifacts folders).
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

Get-ChildItem -Path $root -Include bin, obj -Recurse -Directory -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force

$artifacts = Join-Path $root 'artifacts'
if (Test-Path $artifacts) { Remove-Item $artifacts -Recurse -Force }

Write-Host 'Clean.'
