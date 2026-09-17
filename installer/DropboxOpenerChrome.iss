; SMS-DropboxOpener (Chrome/Edge policy) - Inno Setup 6
; Elevated, one-time machine setup that force-installs the SMS DropboxOpener
; browser extension in Chrome and Edge via the ExtensionSettings policy,
; pointing at the self-hosted CRX. Requires the helper's non-elevated Setup
; (native messaging host) to be installed first.
;
; Silent: /VERYSILENT /SUPPRESSMSGBOXES /NORESTART (works with GPO/RMM)
; Idempotent: rerun rewrites the same two policy values.
; Uninstall removes only this installer's policy keys.

#define AppId "{{7C7A1E52-3F6B-4F79-9ADA-4B64039C1C0A}"
#define AppName "SMS DropboxOpener Chrome/Edge Policy"
#define AppNameNoSpace "SMS-DropboxOpenerChrome"

; Extension ID derived from extension/key.pem (see tools/DerId.cs)
#define ExtId "aimnddiifblldaaacgldllnnnefoahfj"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=SMS Architecture
DefaultDirName={win}\Temp\SMS-DropboxOpenerChrome   ; payload barely matters; no files installed
OutputDir=output
OutputBaseFilename=Setup-{#AppNameNoSpace}-v{#AppVersion}
Compression=lzma2/max
PrivilegesRequired=admin
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyPage=yes
RestartApplications=no

[Files]
; keep installer meaningful with a readme payload
Source: "..\docs\open.html"; DestDir: "{app}"; Flags: ignoreversion deleteafterinstall

[Registry]
; The same self-hosted updates.xml serves Chrome and Edge.
; ExtensionSettings is a dictionary policy stored as a single REG_SZ of JSON.
; Inno string literals: "" == a double quote, {{ == a literal '{'.
Root: HKLM; Subkey: "SOFTWARE\Policies\Google\Chrome\ExtensionSettings"; ValueType: string; ValueName: ""; \
  ValueData: "{{""{#ExtId}"":{{""installation_mode"":""force_installed"",""update_url"":""https://chris4d.github.io/SMS-dropbox-opener/updates.xml""}}"; \
  Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Policies\Microsoft\Edge\ExtensionSettings"; ValueType: string; ValueName: ""; \
  ValueData: "{{""{#ExtId}"":{{""installation_mode"":""force_installed"",""update_url"":""https://chris4d.github.io/SMS-dropbox-opener/updates.xml""}}"; \
  Flags: uninsdeletekey

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
