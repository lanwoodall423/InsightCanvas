# Insight Canvas 2.1.1-rc.1

## Release candidate

This release candidate prepares the 2.1.1 patch release for RimWorld 1.6. It preserves the documented public API and adds release-governance, reproducibility, packaging, and licensing safeguards.

## Compatibility

- RimWorld 1.6.
- Mod package ID: `lan.insightcanvas`.
- Compatible with the documented 2.x public API; no intentional breaking API changes.
- Install Insight Canvas as a separate mod and declare `lan.insightcanvas` as a dependency.
- Consuming mods reference `1.6/Assemblies/InsightCanvas.dll` during compilation and must not redistribute a duplicate framework DLL.

## Installation

1. Download `InsightCanvas-2.1.1-rc.1.zip` and verify its SHA-256 checksum.
2. Extract the archive into the RimWorld `Mods` directory.
3. Enable Insight Canvas in RimWorld's mod list.
4. Place the consuming mod after Insight Canvas when its integration requires the framework.
5. Open **Mod settings > Insight Canvas > Open Feature Showcase** to smoke-test the installation.

## Upgrade notes

Upgrade from the 2.1.x line by replacing the existing Insight Canvas mod directory with this package. Do not merge old assemblies into the new directory and do not leave a second `InsightCanvas.dll` in a consuming mod.

## Known limitations

- The `net472` runtime build requires a local RimWorld 1.6 installation.
- Feature Showcase visual and keyboard accessibility checks require RimWorld interaction and are not fully portable.
- GitHub repository branch protection, metadata, and final publication require owner permissions.

## Validation

Portable tests, API compatibility, version consistency, deterministic package inspection, RimWorld Release build, checked-in binary freshness, and RimTest acceptance validation are required gates. See `Documentation/ReleaseChecklist.md` for the complete matrix.
