# SMS-DropboxOpener

Windows 11 helper that makes `opendbx://` links in the internal Google Sites
open the matching local Dropbox folder in Windows Explorer.

## Sites page authors

Google Sites rejects `opendbx://` URLs in its link editor (it requires a
valid TLD). Instead, link to the bridge page, which redirects to the scheme:

```
https://chris4d.github.io/SMS-dropbox-opener/open.html?p=Projects/Contracts%20Folder
```

- `p` is the path relative to the Dropbox root (`/`-separated, spaces as `%20`).
  An optional leading `business/` or `personal/` segment selects the account
  (default `business`).
- The helper resolves the path against the team root first (`root_path` in
  `info.json`), then the user's own folder, opening whichever matches.
- Works for folders and files (files open in Explorer with `/select`).
- Bad/missing target exits silently with code 3 - the helper never shows UI.

## Security posture

- The URL carries only a filesystem location; the helper never executes
  anything - it only asks `explorer.exe` to navigate (or `/select` a file).
- The bridge page (`docs/open.html`, hosted on GitHub Pages) is a static file
  that only constructs `opendbx://` URLs - it adds no server, endpoints, or
  state. The helper's Dropbox-root sandbox is the security boundary.
- No origin whitelisting is possible at the Windows protocol layer (like
  `mailto:` protocols). Suppress the browser's one-time approval prompt
  fleet-wide with Chrome/Edge policy `AutoLaunchProtocolsFromOrigins`
  scoped to `https://chris4d.github.io`, pushed via GPO.
- Links are sandboxed to the Dropbox roots: traversal (`..`), drive-absolute
  (`C:/...`), UNC (`//server`), and invalid path characters are all rejected
  (exit 3, nothing launched).

## Install / verify / repair

```
.\scripts\build.ps1 -Version X.Y.Z      # also prints SHA-256 digests
# then push tag vX.Y.Z to publish Setup-SMS-DropboxOpener-vX.Y.Z.exe on Releases
```

Workstations install via the SMS-toolkit suite, or standalone:

```
Setup-SMS-DropboxOpener-v0.1.0.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```

Idempotent: rerunning verifies/repairs the install and rewrites the HKCU
protocol registration. Uninstall removes all keys silently.
