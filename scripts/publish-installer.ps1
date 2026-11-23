<#
.SYNOPSIS
    Builds the Windows installer with Inno Setup.
.DESCRIPTION
    Requires Inno Setup 6 (https://jrsoftware.org/isinfo.php). ISCC.exe is looked up on PATH
    and in the default install folders.
    Output: artifacts/installer/Charlie-MJ-Focus-Setup-<version>.exe
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root

try {
    $version = ([xml](Get-Content 'Directory.Build.props')).Project.PropertyGroup.Version | Select-Object -First 1
    $publishDir = Join-Path $root 'artifacts/installer-input'

    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

    # The installer ships a normal (multi-file) self-contained publish; no portable.flag, so data goes to %LOCALAPPDATA%.
    dotnet publish src/CharlieMJFocus.App -c Release -r win-x64 --self-contained true -p:DebugType=none -o $publishDir

    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    $iscc = if ($command) { $command.Source } else { $null }

    if (-not $iscc) {
        $iscc = @(
            "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
            "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
            "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
        ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
    if (-not $iscc) { throw 'Inno Setup 6 was not found. Install it from https://jrsoftware.org/isdl.php' }

    & $iscc "/DAppVersion=$version" "/DSourceDir=$publishDir" "/DOutputDir=$(Join-Path $root 'artifacts/installer')" 'installer/CharlieMJFocus.iss'
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE" }
}
finally {
    Pop-Location
}
