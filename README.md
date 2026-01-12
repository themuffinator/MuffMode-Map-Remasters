# MuffMode Map Remasters

MuffMode Map Remasters is a collection of remastered Quake II maps. The repo is organized into three distinct sections: final versions, under-development versions, and the original maps by the original authors.

## Repository layout

- `finals/maps` - Final, release-ready .bsp maps.
- `finals/src/maps` - Source .map files for final maps.
- `dev/src/maps` - Work-in-progress .map files.
- `dev/maps` - Compiled WIP .bsp outputs (not for release).
- `originals/<map>/pack` - Original map packs as shipped by authors.
- `originals/<map>/web` - Original web metadata and notes.
- `docs` - Original readmes collected for quick reference, plus `docs/final-map-overview.md` for the consolidated final map overview.
- `media` - Screenshots and media for remastered maps.

## Final map list

See `docs/final-map-overview.md` for the current list of final maps, screenshots, and notes.

## Install (manual)

1. Locate your Quake II install directory.
2. Copy all files from `finals/maps` into `<QuakeII>\baseq2\maps` (or `<QuakeII>\rerelease\baseq2\maps` for the remaster).
3. Launch Quake II and load the maps by name.

## Release artifacts

Releases produce two artifacts:

- `MuffMode-Map-Remasters-<version>-final.zip` - A drop-in pack containing `baseq2/maps/*.bsp`.
- `MuffMode-Map-Remasters-<version>-dev.zip` - Development maps package for testers.
- `MuffMode-Map-Remasters-<version>-setup.exe` - Windows installer that auto-detects Steam, GOG, or EOS installs.
- `MuffMode-Map-Remasters-Updater.exe` - Auto-updater for final and dev maps.

## Versioning

Releases use SemVer tags in the form `vMAJOR.MINOR.PATCH`.

- MAJOR: Folder layout or packaging changes that break compatibility.
- MINOR: New remastered maps or significant updates.
- PATCH: Fixes or small content corrections.

The release workflow keys off tags, so create a tag like `v1.2.0` to publish a new release.

## Packaging and release

Local packaging:

```powershell
# Build the updater (self-contained)
powershell -ExecutionPolicy Bypass -File scripts\build-updater.ps1 -Version v1.2.0

# Build the final zip
powershell -ExecutionPolicy Bypass -File scripts\package-release.ps1 -Version v1.2.0

# Build the Windows installer (requires Inno Setup 6)
powershell -ExecutionPolicy Bypass -File scripts\build-installer.ps1 -Version v1.2.0
```

GitHub Actions:

- Tag a release (`vX.Y.Z`).
- The workflow in `.github/workflows/release.yml` builds the zip and installer and attaches them to the release.

## Auto-updater (Windows)

The updater checks GitHub releases, downloads the latest final and dev map packs, and installs them into your Quake II directory. It logs update status and can auto-launch Quake II Rerelease after the update completes.

- Install path detection covers Steam, GOG, and EOS installs.
- Maps install into `rerelease\baseq2\maps` when that folder exists, otherwise `baseq2\maps`.
- Auto-launch preference is stored in `HKCU\Software\MuffMode\MapRemasters\Updater`.

## Credits and licensing

Original maps, assets, and readmes remain the property of their original authors. See `docs` and `originals/<map>/pack/docs` for attribution details.
