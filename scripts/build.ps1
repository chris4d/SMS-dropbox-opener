# Build SMS-DropboxOpener helper + Setup.
# Usage: .\scripts\build.ps1 [-Version X.Y.Z]
# Compiles the helper with the .NET Framework compiler shipped with Windows,
# then compiles the Inno installer. Output: installer\output\Setup-SMS-DropboxOpener-v<Version>.exe
param(
    [string]$Version = '0.1.0'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

# CI passes ref_name (e.g. v0.1.2); strip the v prefix
if ($Version -match '^v') { $Version = $Version.Substring(1) }

function Find-Iscc {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    )
    foreach ($p in $candidates) { if (Test-Path $p) { return $p } }
    throw "Inno Setup 6 (ISCC.exe) not found. Install via: winget install JRSoftware.InnoSetup"
}

# 1) Compile helper (inbox .NET Framework csc — no SDK needed)
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$out = Join-Path $root 'out'
$null = New-Item -ItemType Directory -Force $out
& $csc /nologo /warn:0 /target:winexe /platform:anycpu /out:"$out\DropboxOpener.exe" `
    /reference:System.Web.dll `
    (Join-Path $root 'src\DropboxOpener.cs')
if ($LASTEXITCODE -ne 0) { throw "csc failed" }

# 1b) Native messaging host for the browser extension (console, reads stdio)
& $csc /nologo /warn:0 /target:exe /platform:anycpu /out:"$out\DropboxOpenerHost.exe" `
    (Join-Path $root 'src\NativeHost.cs')
if ($LASTEXITCODE -ne 0) { throw "csc failed (NativeHost)" }

# 2) Compile installers
$iscc = Find-Iscc
& $iscc "/DAppVersion=$Version" (Join-Path $root 'installer\DropboxOpener.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }

# 3) Browser extension CRX (stable ID via committed extension/key.pem)
$chrome = Join-Path $env:ProgramFiles 'Google\Chrome\Application\chrome.exe'
$edge = Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'
$packer = if (Test-Path $chrome) { $chrome } elseif (Test-Path $edge) { $edge } else { throw "Chrome or Edge not found for CRX packaging" }
$extDir = Join-Path $root 'extension'
$key = Join-Path $root 'extension_key.pem'
$dist = Join-Path $root 'dist'
$null = New-Item -ItemType Directory -Force $dist
# pack prints the extension ID; the CRX lands next to the source dir
Push-Location $root
& $packer --pack-extension="$extDir" --pack-extension-key="$key" 2>&1 | Out-Null
Pop-Location
$crx = Join-Path $root 'extension.crx'
if (-not (Test-Path $crx)) { throw "CRX not produced" }
Move-Item $crx (Join-Path $dist 'SMS-DropboxOpener-Chrome.crx') -Force

# 4) updates.xml (self-hosted update manifest served from GitHub Pages docs/)
$updateXml = @"
<?xml version="1.0" encoding="UTF-8"?>
<gupdate xmlns="http://www.google.com/update2/response" protocol="2.0">
  <app appid="aimnddiifblldaaacgldllnnnefoahfj">
    <updatecheck codebase="https://github.com/chris4d/SMS-dropbox-opener/releases/latest/download/SMS-DropboxOpener-Chrome.crx" version="$Version" />
  </app>
</gupdate>
"@
Set-ItemProperty -Path (Join-Path $root 'docs\updates.xml') -Name IsReadOnly -Value $false -ErrorAction SilentlyContinue
$updateXml | Out-File (Join-Path $root 'docs\updates.xml') -Encoding utf8 -Force

# 5) Elevated policy-only installer (Chrome/Edge ExtensionSettings)
& $iscc "/DAppVersion=$Version" (Join-Path $root 'installer\DropboxOpenerChrome.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC failed (policy setup)" }

Write-Host "Done: installer\output\Setup-SMS-DropboxOpener-v$Version.exe"
Write-Host "Done: installer\output\Setup-SMS-DropboxOpenerChrome-v$Version.exe"

# Print SHA-256 of every output installer (for SMS-toolkit digest verification)
Get-ChildItem (Join-Path $root 'installer\output\*.exe') | ForEach-Object {
    (Get-FileHash $_.FullName).Hash
}
