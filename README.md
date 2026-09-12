# SMS-DropboxOpener

Windows 11 helper that makes `opendbx://` links in the internal Google Sites
open the matching local Dropbox folder in Windows Explorer.

Example link for a Sites page:

```
<a href="opendbx://business/Projects">Open Projects in Explorer</a>
```

- `business`/`personal` is an optional first segment (defaults to `business`).
- The rest is the path relative to the Dropbox root. The helper resolves it
  against the team root first (`root_path` in `info.json`), then the user's
  own folder, opening whichever matches.
- Works for folders and files (files open in Explorer with `/select`).
- Bad/missing target exits silently with code 3 — the helper never shows UI.

## Security posture

- The URL carries only a filesystem location; the helper never executes
  anything — it only asks `explorer.exe` to navigate (or `/select` a file).
- No origin whitelisting is possible at the Windows protocol layer (like
  `mailto:` protocols). Suppress the browser's one-time approval prompt
  fleet-wide with Chrome/Edge policy `AutoLaunchProtocolsFromOrigins`
  scoped to the Google Sites origin, pushed via GPO.
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
