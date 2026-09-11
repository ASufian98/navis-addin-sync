# BINA Navis Sync — Installer & Release

## Build a release

```powershell
.\build-release.ps1 -Version 0.0.1 -Notes "First release"
```

Produces in `artifacts/`:
- `BinaNavisSync-0.0.1.zip` — OTA payload
- `version.json` — OTA feed file
- `BinaNavisSync-0.0.1-setup.exe` — installer (if Inno Setup installed)

## Release procedure

1. Build: `.\build-release.ps1 -Version X.Y.Z`
2. Create GitHub Release `vX.Y.Z`
3. Upload:
   - `BinaNavisSync-X.Y.Z.zip`
   - `BinaNavisSync-X.Y.Z-setup.exe`
   - `version.json`
4. Fleet updates at next Navisworks start

## OTA update flow

The plugin checks `version.json` on GitHub Releases:
- Newer version → download zip, verify SHA256, stage
- `mandatory: true` → gates all commands until update staged
- Applied at next Navisworks restart

## Manual install

Copy to Navisworks plugins folder (requires admin):
```
C:\Program Files\Autodesk\Navisworks Manage 2026\Plugins\NavisWebAppSync\
  NavisWebAppSync.dll
  BinaRibbon.xaml
  Newtonsoft.Json.dll
  Resources\
    login.png
    download.png
    upload.png
    info.png
```
