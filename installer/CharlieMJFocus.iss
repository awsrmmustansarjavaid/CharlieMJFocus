; Inno Setup script for Charlie MJ Focus.
; Do not run directly: scripts/publish-installer.ps1 supplies AppVersion, SourceDir and OutputDir.

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\installer-input"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

#define AppName "Charlie MJ Focus"
#define AppExe  "Charlie MJ Focus.exe"

[Setup]
; A fixed AppId lets new versions upgrade the old one in place. Never change it after release.
AppId={{7B3F2C1E-5A64-4D1B-9C80-3E2A91D4F6B7}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Charlie MJ
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=Charlie-MJ-Focus-Setup-{#AppVersion}
SetupIconFile=..\assets\CharlieMJFocus.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Per-user install: no administrator prompt. The user may still choose "all users" in the wizard.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

; NOTE: there is intentionally no [UninstallDelete] section for %LOCALAPPDATA%\CharlieMJFocus.
; Uninstalling or upgrading must never delete the user's tasks, sessions or backups.
