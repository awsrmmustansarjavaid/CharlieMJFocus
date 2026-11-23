# Build and release guide

## Artifact names

- Portable ZIP: `Charlie-MJ-Focus-<version>-portable-win-x64.zip`
- Portable executable inside the ZIP: `Charlie MJ Focus.exe`
- Installer: `Charlie-MJ-Focus-Setup-<version>.exe`

## Local build (Windows)

1. Install .NET 10 SDK.
2. Run `./scripts/build.ps1`.
3. Run `./scripts/publish-portable.ps1` for the portable package.
4. Install Inno Setup 6 and run `./scripts/publish-installer.ps1` for Setup.exe.
5. Inspect files under `artifacts/` before distributing them.

## GitHub release

1. Push the repository to GitHub and ensure the default branch is `main`.
2. Confirm Actions is enabled and the workflow is allowed to create releases with `contents: write` on the release job.
3. Push a semantic version tag, for example `v0.2.0`.
4. Wait for `verify`, `portable` and `installer` jobs to pass.
5. Confirm the GitHub Release contains the portable ZIP and Setup.exe.
6. Test both downloads on a clean Windows x64 VM.

The workflow uses GitHub-hosted Windows runners and installs Inno Setup through Chocolatey. A release is not published if either package job fails.

## Signing

The current build is unsigned. To reduce SmartScreen warnings for public distribution, obtain a trusted code-signing certificate and add signing to the Windows packaging pipeline using GitHub Actions secrets / a protected signing environment. Never commit a `.pfx`, private key, password or signing token to the repository.

## Data safety checks

- Installer upgrade must preserve `%LOCALAPPDATA%\CharlieMJFocus\`.
- Uninstall must not remove the user database or backups.
- Portable mode must keep its `Data` folder beside the portable EXE while `portable.flag` exists.
- Test database backup and restore before each release that changes the schema.
