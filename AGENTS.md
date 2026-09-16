# Guidance for AI agents working on this repo.

## Purpose
Windows 11 helper + setup that adds an `opendbx://` URL protocol. Links on
the office's internal Google Sites (e.g. `opendbx://business/Projects`)
open the corresponding local Dropbox folder in Windows Explorer. Dropbox
provides no official third-party "open in Explorer" API; the helper finds
the local Dropbox root from `%LOCALAPPDATA%`/`%APPDATA%`
`Dropbox\info.json` (`path` and `root_path` members) and resolves the
relative path against team root first, then the user's own folder.

## Contract (SMS-toolkit suite)
- Publishes `Setup-SMS-DropboxOpener-vX.Y.Z.exe` to GitHub Releases (tag push).
- Inno `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART` supported, idempotent.
- Non-elevated (`PrivilegesRequired=lowest`); protocol registered in HKCU only.
- Suite never imports this code; it stages the Setup.exe (SHA-256 verified).
- No install payload needs admin, no GPO-dependent behavior.

## Build
`.\scripts\build.ps1 [-Version X.Y.Z]` → `installer\output\Setup-SMS-DropboxOpener-v<Version>.exe`
The helper compiles with the inbox .NET Framework `csc.exe` (no SDK needed);
the installer compiles with Inno Setup 6 (`winget install JRSoftware.InnoSetup`).
CI (`.github\workflows\build-setup.yml`) runs the same scripts on windows-latest.

## Helper details
- Source of truth: `src\DropboxOpener.cs`.
- Exit codes: 0 opened, 1 internal error, 2 bad scheme, 3 target not found.
- `business` is the default account; supports `%20`-encoded URL paths;
  best-effort arg join handles unquoted callers (browsers quote `%1`).
- Google Sites rejects `opendbx://` URLs in its link editor, so Sites pages
  link through the GitHub Pages bridge `docs\open.html`
  (`https://chris4d.github.io/SMS-dropbox-opener/open.html?p=<path>`); it
  phone-homes nothing and only forwards to the scheme.

## When committing
- One logical commit per change.
- Do not stage anything under `out/` or `installer/output/`.
- Version bumps flow through `scripts\build.ps1 -Version X.Y.Z` only;
  releases are created by pushing a `v*` tag.
