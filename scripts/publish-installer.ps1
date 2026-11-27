<#
.SYNOPSIS
    Builds the Windows installer (Setup.exe) with Inno Setup.
.DESCRIPTION
    Requires Inno Setup 6 (https://jrsoftware.org/isdl.php). ISCC.exe is looked up on PATH and in the
    default install folders. GitHub Actions installs it with Chocolatey.
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
    $outputDir  = Join-Path $root 'artifacts/installer'

    foreach ($path in @($publishDir, $outputDir)) {
        if (Test-Path $path) { Remove-Item $path -Recurse -Force }
    }

    # The installer ships a normal self-contained publish. There is no portable.flag, so installed copies
    # keep their data in %LOCALAPPDATA%\CharlieMJFocus, which survives upgrades and uninstalls.
    dotnet publish src/CharlieMJFocus.App -c Release -r win-x64 --self-contained true -p:DebugType=none -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
    if (-not (Test-Path (Join-Path $publishDir 'CharlieMJFocus.exe'))) { throw 'CharlieMJFocus.exe was not produced.' }

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

    & $iscc "/DAppVersion=$version" "/DSourceDir=$publishDir" "/DOutputDir=$outputDir" 'installer/CharlieMJFocus.iss'
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE" }

    $setup = Get-ChildItem $outputDir -Filter '*.exe' | Select-Object -First 1
    if (-not $setup) { throw 'Inno Setup finished but no Setup.exe was produced.' }
    Write-Host ("Installer: {0} ({1:N1} MB)" -f $setup.FullName, ($setup.Length / 1MB))
}
finally {
    Pop-Location
}
