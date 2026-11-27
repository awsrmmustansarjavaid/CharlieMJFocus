<#
.SYNOPSIS
    Builds the portable edition: a single self-contained EXE (no .NET install needed) plus a portable.flag file.
.DESCRIPTION
    Output folder: artifacts/portable/Charlie-MJ-Focus/
        Charlie-MJ-Focus.exe   the application
        portable.flag          makes the app keep all data in a "Data" folder next to the EXE
        README-portable.txt
    Output zip:    artifacts/portable/Charlie-MJ-Focus-<version>-portable-win-x64.zip
    The compiled file is named CharlieMJFocus.exe internally (no spaces, safe for WPF resources); it is
    renamed here to the final user-facing name.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

try {
    $version = ([xml](Get-Content 'Directory.Build.props')).Project.PropertyGroup.Version | Select-Object -First 1
    $work = Join-Path $root 'artifacts/portable/_publish'
    $out  = Join-Path $root 'artifacts/portable/Charlie-MJ-Focus'
    $zip  = Join-Path $root "artifacts/portable/Charlie-MJ-Focus-$version-portable-win-x64.zip"

    foreach ($path in @($work, $out, $zip)) {
        if (Test-Path $path) { Remove-Item $path -Recurse -Force }
    }

    dotnet publish src/CharlieMJFocus.App -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=none `
        -o $work
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

    $built = Join-Path $work 'CharlieMJFocus.exe'
    if (-not (Test-Path $built)) { throw "Expected $built was not produced." }

    # Copy everything the publish produced (normally just the EXE), then give the EXE its final name.
    New-Item -ItemType Directory -Path $out | Out-Null
    Get-ChildItem $work -File | Where-Object { $_.Extension -ne '.pdb' } | Copy-Item -Destination $out
    Rename-Item (Join-Path $out 'CharlieMJFocus.exe') 'Charlie-MJ-Focus.exe'

    # Marks this copy as portable: data lives in .\Data next to the EXE.
    Set-Content -Path (Join-Path $out 'portable.flag') -Value 'Delete this file to store data in %LOCALAPPDATA% instead.'

    Set-Content -Path (Join-Path $out 'README-portable.txt') -Value @"
Charlie MJ Focus $version (portable)

1. Double-click Charlie-MJ-Focus.exe. Nothing needs to be installed.
2. All your data is stored in the "Data" folder next to the EXE. Copy the whole folder to move or back it up.
3. Delete portable.flag if you would rather store data in %LOCALAPPDATA%\CharlieMJFocus.
4. Windows SmartScreen may say "unknown publisher" because the file is not code-signed:
   choose More info, then Run anyway.

Shortcuts (work from any app): Ctrl+Alt+P start/pause, Ctrl+Alt+D count a distraction.
"@

    Compress-Archive -Path (Join-Path $out '*') -DestinationPath $zip
    Remove-Item $work -Recurse -Force

    $exe = Get-Item (Join-Path $out 'Charlie-MJ-Focus.exe')
    Write-Host ("Portable EXE: {0} ({1:N1} MB)" -f $exe.FullName, ($exe.Length / 1MB))
    Write-Host "Portable zip: $zip"
}
finally {
    Pop-Location
}
