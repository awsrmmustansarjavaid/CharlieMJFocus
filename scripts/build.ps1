<#
.SYNOPSIS
    Restores, builds and tests Charlie MJ Focus.
.EXAMPLE
    ./scripts/build.ps1                # Release build + tests
    ./scripts/build.ps1 -Configuration Debug -SkipTests
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

try {
    dotnet restore CharlieMJFocus.sln
    dotnet build CharlieMJFocus.sln -c $Configuration --no-restore

    if (-not $SkipTests) {
        dotnet test CharlieMJFocus.sln -c $Configuration --no-build
    }
}
finally {
    Pop-Location
}
