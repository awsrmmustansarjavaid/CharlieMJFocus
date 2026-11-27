<#
.SYNOPSIS
    Launches the published portable app several times (different pages and themes), takes a screenshot of
    each, and fails if the app exits early or writes any error to its log.
.DESCRIPTION
    Used by the GitHub Actions "smoke-test" job, but it also works on any Windows PC:
        ./scripts/smoke-test.ps1 -AppDir artifacts/portable/Charlie-MJ-Focus
    The app runs in portable mode, so its data and logs stay inside -AppDir\Data.
    Screenshots are best-effort (they need an interactive desktop); the crash and log checks are not.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AppDir,
    [string]$OutDir = 'smoke-out',
    [int]$WaitSeconds = 8
)

$ErrorActionPreference = 'Stop'
$AppDir = (Resolve-Path $AppDir).Path
$exe = Join-Path $AppDir 'Charlie-MJ-Focus.exe'
if (-not (Test-Path $exe)) { throw "Charlie-MJ-Focus.exe was not found in $AppDir" }
if (-not (Test-Path (Join-Path $AppDir 'portable.flag'))) { throw 'portable.flag is missing, so the test would use real user data.' }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Add-Type -AssemblyName System.Windows.Forms, System.Drawing

function Save-Screenshot([string]$Path) {
    try {
        $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $bitmap = New-Object System.Drawing.Bitmap $bounds.Width, $bounds.Height
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
        $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
        $graphics.Dispose(); $bitmap.Dispose()
    }
    catch { Write-Warning "Screenshot failed: $($_.Exception.Message)" }
}

# The first run also fills the empty database with sample data (--demo) so every page has content.
$runs = @(
    @{ Name = '01-dashboard';        Args = @('--demo', '--page=Dashboard',  '--theme=Light') },
    @{ Name = '02-timer';            Args = @('--page=Timer',      '--theme=Light') },
    @{ Name = '03-tasks';            Args = @('--page=Tasks',      '--theme=Light') },
    @{ Name = '04-planner';          Args = @('--page=Planner',    '--theme=Light') },
    @{ Name = '05-analytics';        Args = @('--page=Statistics', '--theme=Light') },
    @{ Name = '06-appearance';       Args = @('--page=Appearance', '--theme=Light') },
    @{ Name = '07-settings';         Args = @('--page=Settings',   '--theme=Light') },
    @{ Name = '08-dashboard-dark';   Args = @('--page=Dashboard',  '--theme=Dark') },
    @{ Name = '09-planner-dark-glass'; Args = @('--page=Planner',  '--theme=Dark', '--backdrop=Mica') }
)

$failures = @()

foreach ($run in $runs) {
    Write-Host "Launching $($run.Name): $($run.Args -join ' ')"
    $process = Start-Process -FilePath $exe -ArgumentList $run.Args -PassThru
    Start-Sleep -Seconds $WaitSeconds

    if ($process.HasExited) {
        $failures += "$($run.Name): the app exited by itself with code $($process.ExitCode)"
        continue
    }

    Save-Screenshot (Join-Path $OutDir "$($run.Name).png")
    Stop-Process -Id $process.Id -Force
    $process.WaitForExit()
}

# Collect the logs and fail if the app wrote any error.
$logDir = Join-Path $AppDir 'Data/Logs'
if (Test-Path $logDir) {
    Copy-Item (Join-Path $logDir '*.txt') -Destination $OutDir -ErrorAction SilentlyContinue
    $errors = Select-String -Path (Join-Path $logDir '*.txt') -Pattern '\[ERROR\]' -Context 0, 6
    if ($errors) {
        $failures += "The log contains $($errors.Count) error(s):"
        $errors | ForEach-Object { $failures += ($_.Line + "`n" + ($_.Context.PostContext -join "`n")) }
    }
}
else {
    $failures += "No log folder was created at $logDir (did the app start at all?)"
}

if ($failures) {
    $failures | ForEach-Object { Write-Host "::error::$_" }
    throw "Smoke test failed with $($failures.Count) problem(s)."
}

Write-Host "Smoke test passed: $($runs.Count) launches, no crashes, no logged errors."
