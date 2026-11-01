# Insight Canvas 2.2.0 publication plan — pending validation

The previous 2.1.1 release documentation describes RimWorld 1.6. It remains archival and **must not be reused** for the 1.7 Waylith release. This 2.2.0 plan is prospective: no 1.7 DLL or release ZIP has been built, no checksum has been calculated, no tag exists, and no GitHub release has been authorized.

## Planned release candidate (not built)

- Candidate tag: `v2.2.0-rc.1` — **not created**
- Candidate package: `InsightCanvas-2.2.0-rc.1.zip` — **not built**
- Candidate checksum: `InsightCanvas-2.2.0-rc.1.zip.sha256` — **not generated**
- Candidate notes: [`ReleaseNotes-2.2.0.md`](ReleaseNotes-2.2.0.md) — draft
- Required game: RimWorld 1.7 Waylith (beta as of October 2026)
- Engine modes: GitHub for development and pull-request review; Local / ProjectNexus for validation

## Planned stable publication (not approved)

- Tag: `v2.2.0` — **not created**
- Package: `InsightCanvas-2.2.0.zip` — **not built**
- Checksum: `InsightCanvas-2.2.0.zip.sha256` — **not generated**

The 2.2.0 release requires the freshly compiled `1.7/Assemblies/InsightCanvas.dll` and `InsightCanvas.xml` produced from the same exact approved source commit; neither historical 2.1.1 DLLs nor 1.6 package artifacts are acceptable.

## Release gates and owner decisions

1. Review the Waylith 1.7 SDK and compile all framework and independent consumer projects with the installed .NET 10 SDK and game `Assembly-CSharp.dll`, `Waylith.dll`, and `UnityStubs.dll`. A missing 1.7 SDK or bridge dependency is **BLOCKED**, not a pass.
2. Inspect the exact GitHub source candidate. ProjectNexus may pin an unmerged PR but **must not** execute its code on the credential-bearing Windows host without separately approved isolation. Deleting legacy 1.6 artifacts may be outside its initial PR source-scanner contract; do not weaken the deletion/symlink safety gate to obtain a green result.
3. Once the exact reviewed code is in a permitted trusted source state, use Local / ProjectNexus to run its locally approved named checks. GitHub Actions is not the selected validation engine. Do not grant check trust on behalf of the developer.
4. Rebuild the runtime DLL/XML under `1.7/Assemblies`, pass API/version/package/freshness checks, and capture exact artifact hashes and dependencies.
5. Validate the authenticated ten-page Feature Showcase under the existing RimLiaison/DevBridge2 runtime lifecycle only after verifying 1.7-compatible RimBridgeServer and game installation. Preserve the owner-controlled profile, leases, and cleanup.
6. Capture release notes, artifact SHA-256, successful local validation evidence and manual acceptance against the exact candidate. Only then request explicit owner approval for RC or stable publication. Approval, merge, installation and release are independent decisions.

## Compatibility statement (planned)

Insight Canvas 2.2.0 targets **RimWorld 1.7 only** with package ID `lan.insightcanvas`, a `.NET 10` runtime and the Waylith UnityStubs compatibility layer. It retains the documented composable public API, but no longer claims support for RimWorld 1.6.

## Historical archive

Earlier 1.6-era plans, package hashes and notes are archived in [`ReleaseNotes-2.1.1.md`](ReleaseNotes-2.1.1.md) and [`ReleaseNotes-2.1.1-rc.1.md`](ReleaseNotes-2.1.1-rc.1.md). These are not certified Waylith build artifacts.
