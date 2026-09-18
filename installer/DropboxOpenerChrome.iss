; SMS-DropboxOpener (Chrome/Edge policy) - Inno Setup 6
; Elevated, one-time machine setup that force-installs the SMS DropboxOpener
; browser extension in Chrome and Edge via the ExtensionSettings policy,
; pointing at the self-hosted CRX update manifest. Requires the helper's
; non-elevated Setup (native messaging host) to be installed first.
;
; Silent: /VERYSILENT /SUPPRESSMSGBOXES /NORESTART (works with GPO/RMM)
; Idempotent: rerun rewrites the same policy values.
; Uninstall removes only this installer's own values.

#define AppId "{{7C7A1E52-3F6B-4F79-9ADA-4B64039C1C0A}"
#define AppName "SMS DropboxOpener Chrome/Edge Policy"
#define AppNameNoSpace "SMS-DropboxOpenerChrome"

; Extension ID derived from extension_key.pem (see tools/DerId.cs)
#define ExtId "mcedhcfdcbpampgjgbchfhaafpefbfbl"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=SMS Architecture
DefaultDirName={win}\Temp\SMS-DropboxOpenerChrome
OutputDir=output
OutputBaseFilename=Setup-{#AppNameNoSpace}-v{#AppVersion}
Compression=lzma2/max
PrivilegesRequired=admin
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyPage=yes
RestartApplications=no

[Files]
Source: "..\docs\open.html"; DestDir: "{app}"; Flags: ignoreversion deleteafterinstall

[Registry]
; ExtensionSettings is a DICTIONARY policy. On Windows its dict keys are the
; VALUE NAMES under the policy key, so each extension gets a value NAMED by
; its extension ID whose data is that extension's JSON settings:
;
;   HKLM\...\ExtensionSettings
;       <value name = extension id> = {"installation_mode":"force_installed",
;                                     "update_url":"https://.../updates.xml"}
;
; Schema rules proven empirically via edge://policy validation errors:
;   - a value at the (default) name is read as dict key "" -> "Unknown property: "
;   - "install_url" is not in the schema -> "Unknown property: install_url"
;   - "update_url" is REQUIRED for force_installed entries (self-hosted update
;     manifest; the CRX download URL lives inside that XML).
; Inno escaping inside ValueData: "" = double quote, {{ = literal '{'.

Root: HKLM; Subkey: "SOFTWARE\Policies\Google\Chrome\ExtensionSettings"; ValueType: string; ValueName: "{#ExtId}"; \
  ValueData: "{{""installation_mode"":""force_installed"",""update_url"":""https://chris4d.github.io/SMS-dropbox-opener/updates.xml""}"; \
  Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\Policies\Microsoft\Edge\ExtensionSettings"; ValueType: string; ValueName: "{#ExtId}"; \
  ValueData: "{{""installation_mode"":""force_installed"",""update_url"":""https://chris4d.github.io/SMS-dropbox-opener/updates.xml""}"; \
  Flags: uninsdeletevalue

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
// Uninstall of ExtensionSettings is surgical by construction: our entries are
// NAMESPACED (value name = our extension ID), so uninsdeletevalue can never
// touch another extension's entry. The only manual cleanup needed is for the
// ExtensionInstallForcelist entries written by earlier experimental builds
// (value name "1", data "<id>;<url>") - deleted only if the data is ours.
procedure DeleteOurForcelistValue(const SubKey: string);
var
  Data: string;
begin
  if RegQueryStringValue(HKEY_LOCAL_MACHINE, SubKey, '1', Data) then
    if Pos('{#ExtId};', Data) = 1 then begin
      RegDeleteValue(HKEY_LOCAL_MACHINE, SubKey, '1');
      Log('Removed our ExtensionInstallForcelist entry under ' + SubKey);
    end else
      Log('Slot "1" under ' + SubKey + ' belongs to another extension; left untouched');
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then begin
    DeleteOurForcelistValue('SOFTWARE\Policies\Google\Chrome\ExtensionInstallForcelist');
    DeleteOurForcelistValue('SOFTWARE\Policies\Microsoft\Edge\ExtensionInstallForcelist');
  end;
end;
