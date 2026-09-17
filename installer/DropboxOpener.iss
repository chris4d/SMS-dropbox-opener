; SMS-DropboxOpener - Inno Setup 6
; Registers the opendbx:// protocol per-user (no elevation) and installs the
; helper that resolves the local Dropbox folder from Dropbox's own info.json
; and opens the target path in Windows Explorer.
;
; Contract (SMS-toolkit suite):
; - publishes Setup-SMS-DropboxOpener-vX.Y.Z.exe to GitHub Releases
; - /VERYSILENT /SUPPRESSMSGBOXES /NORESTART supported
; - ARP entry written by Inno automatically
; - idempotent: rerun replaces files and rewrites registry keys
; - needs no elevation (PrivilegesRequired=lowest)

#define AppId "{{01B6FCE1-1E6F-4634-877B-B34E7B67D536}"
#define AppName "SMS DropboxOpener"
#define AppNameNoSpace "SMS-DropboxOpener"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=SMS Architecture
DefaultDirName={localappdata}\SMS-DropboxOpener
OutputDir=output
OutputBaseFilename=Setup-{#AppNameNoSpace}-v{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyPage=yes
RestartApplications=no

[Files]
Source: "..\out\DropboxOpener.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\out\DropboxOpenerHost.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "com.sms.dropboxopener.json"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
; URL Protocol handler - HKCU so no elevation is needed. All keys removed on uninstall.
Root: HKCU; Subkey: "Software\Classes\opendbx"; ValueType: string; ValueName: ""; ValueData: "URL:Open Dropbox Folder"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\opendbx"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Classes\opendbx\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\DropboxOpener.exe"" ""%1"""; Flags: uninsdeletekey

; Native messaging host registrations (per-user, Chrome + Edge) so the browser
; extension can reach the helper. Manifest is in {app}; its JSON "path" is
; relative to that directory per Chromium's Windows rule.
Root: HKCU; Subkey: "Software\Google\Chrome\NativeMessagingHosts\com.sms.dropboxopener"; ValueType: string; ValueName: ""; ValueData: "{app}\com.sms.dropboxopener.json"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Microsoft\Edge\NativeMessagingHosts\com.sms.dropboxopener"; ValueType: string; ValueName: ""; ValueData: "{app}\com.sms.dropboxopener.json"; Flags: uninsdeletekey

[Run]
Filename: "{app}\DropboxOpener.exe"; Flags: runhidden

[Icons]

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
