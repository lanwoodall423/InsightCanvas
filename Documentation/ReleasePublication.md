# Insight Canvas publication plan

This plan is prepared for owner approval. It does not create a Git tag or GitHub Release.

## Release candidate

- Proposed tag: `v2.1.1-rc.1`
- Title: `Insight Canvas 2.1.1-rc.1`
- Package: `InsightCanvas-2.1.1-rc.1.zip`
- Checksum: `InsightCanvas-2.1.1-rc.1.zip.sha256`
- SHA-256: `1ea944d9f437bf7e683e2a97c6e8bcbc0050b681e0ff53dd991ad41567cc7b17`
- Notes: [`ReleaseNotes-2.1.1-rc.1.md`](ReleaseNotes-2.1.1-rc.1.md)

## Proposed stable publication

- Proposed tag: `v2.1.1`
- Title: `Insight Canvas 2.1.1`
- Package: `InsightCanvas-2.1.1.zip`
- Checksum: `InsightCanvas-2.1.1.zip.sha256`
- SHA-256: `1ea944d9f437bf7e683e2a97c6e8bcbc0050b681e0ff53dd991ad41567cc7b17`
- Notes: [`ReleaseNotes-2.1.1.md`](ReleaseNotes-2.1.1.md)

## Owner publication sequence

1. Review and merge the prepared changes through the protected `main` pull request.
2. Confirm GitHub description, topics, GPL-3.0 recognition, branch rules, and passing CI.
3. Publish `v2.1.1-rc.1` with the RC package and checksum when an RC review is desired.
4. Re-run the release checklist against the exact approved commit and RC install.
5. After explicit stable approval, create `v2.1.1` and attach the stable package and checksum.
6. Replace the `Unreleased` changelog section with the approved dated `2.1.1` entry at stable publication time.

## Compatibility statement

Insight Canvas 2.1.1 targets RimWorld 1.6, uses package ID `lan.insightcanvas`, preserves the documented public API, and requires consuming mods to install the framework separately without bundling a duplicate `InsightCanvas.dll`.
