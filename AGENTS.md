# AGENTS.md

This file orients contributors and automation to the MuffMode Map Remasters repository.

## Mission

- Preserve original map packs in `originals/`.
- Maintain work-in-progress remasters in `dev/`.
- Publish release-ready remasters from `finals/`.

## Structure rules

- Final maps live in `finals/maps` with matching sources in `finals/src/maps`.
- WIP sources live in `dev/src/maps`; compiled test builds go in `dev/maps`.
- Do not modify files under `originals/` unless you are fixing archival metadata.
- Keep map filenames stable and lowercase (existing `mm-` prefix).

## Documentation rules

- User-facing docs (including `README.md` and `docs/final-map-overview.md`) should be
  written in a human-friendly tone with clean, modern formatting.

## Release process

1. Ensure all final maps are in `finals/maps` and their sources are in `finals/src/maps`.
2. Update screenshots in `media/` as needed.
3. Build the updater with `scripts/build-updater.ps1`.
4. Package the release with `scripts/package-release.ps1` (final + dev zips).
5. Build the Windows installer with `scripts/build-installer.ps1`.
6. Tag the release `vMAJOR.MINOR.PATCH` to trigger `.github/workflows/release.yml`.

## Tooling

- Packaging script: `scripts/package-release.ps1`
- Installer build script: `scripts/build-installer.ps1`
- Updater build script: `scripts/build-updater.ps1`
- Installer definition: `installer/muffmode-remasters.iss`
- Release workflow: `.github/workflows/release.yml`
- Updater source: `tools/updater`

## Installer behavior

- Detects Steam, GOG, and EOS install locations.
- Installs final maps into `<QuakeII>\baseq2\maps` by default.
- Includes the auto-updater in the install root.

If detection fails, the installer should still allow a manual path override.
