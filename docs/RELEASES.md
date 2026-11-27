# Releases and downloads

## What the workflow produces

`.github/workflows/build.yml` ("Build, test and package") runs on every push to `main`, on pull requests, and on demand
(**Actions → Build, test and package → Run workflow**).

| Job | Output | Artifact name |
| --- | --- | --- |
| `verify` | Build + all tests | `Charlie-MJ-Focus-Test-Results` |
| `portable` | `Charlie-MJ-Focus.exe` + `portable.flag` + `README-portable.txt` | `Charlie-MJ-Focus-Windows-Portable` |
| `installer` | `Charlie-MJ-Focus-Setup-<version>.exe` | `Charlie-MJ-Focus-Windows-Installer` |
| `smoke-test` | Screenshots of every page, app logs | `Charlie-MJ-Focus-UI-Screenshots` |
| `release` | A GitHub Release (version tags only) | the portable `.zip` and the `Setup.exe` |

`portable` and `installer` only run after `verify` succeeds. `release` needs both packages but does not wait for the
screenshot job, so a flaky screenshot can never block a release.

## Downloading a build

1. Repository → **Actions** → open the newest run with a green tick.
2. Scroll to **Artifacts** at the bottom of the page.
3. Download `Charlie-MJ-Focus-Windows-Portable` (unzip, run `Charlie-MJ-Focus.exe`) or
   `Charlie-MJ-Focus-Windows-Installer` (unzip, run the Setup).

GitHub zips every artifact, so the download is a `.zip` containing the `.exe`. Artifacts expire after 30 days.
For permanent downloads, publish a release.

## Publishing a release

```powershell
# 1. Edit <Version> in Directory.Build.props and add notes to CHANGELOG.md, commit, push.
# 2. Tag and push:
git tag v0.1.0
git push origin v0.1.0
```

The `release` job then creates the release with notes generated from your commits and attaches:

* `Charlie-MJ-Focus-v0.1.0-portable-win-x64.zip` (contains `Charlie-MJ-Focus.exe`)
* `Charlie-MJ-Focus-Setup-0.1.0.exe`

The tag must start with `v`. The version inside the installer comes from `Directory.Build.props`, so keep it in step with the tag.

## Building locally

```powershell
./scripts/publish-portable.ps1     # artifacts/portable/
./scripts/publish-installer.ps1    # artifacts/installer/ (needs Inno Setup 6)
./scripts/smoke-test.ps1 -AppDir artifacts/portable/Charlie-MJ-Focus
```

## File names

The compiled program is called `CharlieMJFocus.exe` (no spaces, which keeps WPF resource paths and scripts safe).
The portable script renames it to `Charlie-MJ-Focus.exe`. What users see in file properties, Task Manager, the Start
Menu and the installer is **"Charlie MJ Focus"**, set in the project metadata.

## Code signing

The files are unsigned, so Windows SmartScreen shows "unknown publisher" until the app builds reputation. To remove the
warning you need a code-signing certificate; add a signing step to the `portable` and `installer` jobs (for example with
`signtool`) and store the certificate in GitHub Secrets.
