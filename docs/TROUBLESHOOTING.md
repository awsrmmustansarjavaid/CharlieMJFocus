# Troubleshooting

## The downloaded zip has no `.exe` in it (only README files)

First find out whether the `.exe` was **never built** or was **removed on your PC**.

1. Open the Actions run → click the **Portable** (or **Installer**) job → its **Summary** page.
   Every package job lists each file with its size and SHA-256 fingerprint, and **fails (red) if the `.exe` is missing or
   too small**. A green job with a ~60-150 MB `.exe` listed means the file was built correctly.
2. If it was built but your zip lacks it, **Windows Security removed it**. Unsigned, freshly built programs are often
   flagged by mistake ("false positive"), and the file silently disappears when you unzip:
   * Open **Windows Security → Virus & threat protection → Protection history**.
   * Find `Charlie-MJ-Focus.exe` (or the Setup file), choose **Actions → Restore** (or **Allow on device**).
   * To avoid it next time: right-click the downloaded `.zip` → **Properties → Unblock → OK**, *then* extract it,
     or add the extraction folder under **Virus & threat protection settings → Exclusions**.
3. Check the file is genuine: `Get-FileHash .\Charlie-MJ-Focus.exe` must equal the hash in `SHA256SUMS.txt`
   (included in each package) and on the run's summary page.

The long-term fix is **code signing** (see `docs/RELEASES.md`). Until then the safest route is to build it yourself.

## Build the EXE on your own PC (no GitHub needed)

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. In PowerShell, inside the repository folder:
   ```powershell
   ./scripts/publish-portable.ps1
   ```
3. Your program is at `artifacts\portable\Charlie-MJ-Focus\Charlie-MJ-Focus.exe`.

For the installer you also need [Inno Setup 6](https://jrsoftware.org/isdl.php), then `./scripts/publish-installer.ps1`
(result in `artifacts\installer\`).

## Windows says "unknown publisher" / SmartScreen blocks it

Choose **More info → Run anyway**. This appears for every unsigned program.

## The app does not start

Open the log: `%LOCALAPPDATA%\CharlieMJFocus\Logs\log-<date>.txt` (portable: `Data\Logs` next to the EXE) and send the
last `[ERROR]` entry. The Actions run also uploads screenshots and logs as `Charlie-MJ-Focus-UI-Screenshots`.

## Where did my data go?

Installed: `%LOCALAPPDATA%\CharlieMJFocus\`. Portable: the `Data` folder next to the EXE (if `portable.flag` is present).
Backups are in the `Backups` folder beside the database.
