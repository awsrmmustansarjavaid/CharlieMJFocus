<#
.SYNOPSIS
    Builds the portable edition: one self-contained EXE plus a "portable.flag" file.
.DESCRIPTION
    The flag file tells the app to keep all of its data in a "Data" folder next to the EXE
    instead of %LOCALAPPDATA%, so the whole folder can live on a USB stick.
    Output: artifacts/portable/CharlieMJFocus-<version>-portable-win-x64.zip
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

try {
    $version = ([xml](Get-Content 'Directory.Build.props')).Project.PropertyGroup.Version | Select-Object -First 1
    $out = Join-Path $root 'artifacts/portable/CharlieMJFocus'

    if (Test-Path $out) { Remove-Item $out -Recurse -Force }

    dotnet publish src/CharlieMJFocus.App -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=none `
        -o $out

    # Marks this copy as portable.
    Set-Content -Path (Join-Path $out 'portable.flag') -Value 'Delete this file to store data in %LOCALAPPDATA% instead.'

    $zip = Join-Path $root "artifacts/portable/CharlieMJFocus-$version-portable-win-x64.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip

    Write-Host "Portable build ready: $zip"
}
finally {
    Pop-Location
}
