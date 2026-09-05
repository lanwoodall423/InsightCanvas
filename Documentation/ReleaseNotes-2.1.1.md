# Insight Canvas 2.1.1

## Release

Insight Canvas 2.1.1 is a patch release for RimWorld 1.6. It preserves the documented public API while making the project ready for reproducible validation and publication.

## Compatibility

- RimWorld 1.6.
- Mod package ID: `lan.insightcanvas`.
- Assembly version: `2.1.1.0`.
- GPLv3 licensed; see `LICENSE`.
- Install Insight Canvas separately. Consumers declare `lan.insightcanvas`, reference the installed `InsightCanvas.dll`, and do not bundle a duplicate framework DLL.

## Installation

1. Download `InsightCanvas-2.1.1.zip` and verify `InsightCanvas-2.1.1.zip.sha256`.
2. Extract the archive into the RimWorld `Mods` directory.
3. Enable Insight Canvas in RimWorld's mod list.
4. Load dependent mods after Insight Canvas where required.
5. Open **Mod settings > Insight Canvas > Open Feature Showcase**.

## Upgrade and migration

Replace the previous Insight Canvas directory rather than merging assemblies. Remove any duplicate `InsightCanvas.dll` from consuming mod packages. No intentional public API migration is required for 2.1.1.

## Known limitations

- Runtime compilation requires RimWorld 1.6's local `net472` managed assemblies.
- Manual visual, keyboard, accessibility, and clean-install gates require an interactive RimWorld session.

## Validation

The publication candidate must pass the portable Release harness, API and version checks, zero-warning RimWorld Release build, checked-in artifact freshness, RimTest acceptance recipe, canonical package inspection, and SHA-256 verification. The final validation matrix is recorded with the owner-approved publication.
