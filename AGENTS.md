# AGENTS.md

## Project Type

This repository is a **framework mod**. Apply the global development/tooling contract.

## Framework-Specific Rules

Framework changes may affect downstream mods and public integration contracts.

* Allow RimTest to select downstream compatibility/integration coverage.
* Do not treat framework-local tests as sufficient when affected consumers require validation.
* Preserve public APIs, schemas, serialization, hooks, and integration behavior unless the task intentionally changes them.
* Do not manually narrow conservative affected-test selection.
* Breaking compatibility must be intentional and reported clearly.

For source changes, use the normal RimTest-owned workflow defined by the global `AGENTS.md`.

## RimWorld 1.7-only target and validation

Insight Canvas targets RimWorld 1.7 / Waylith only; historical 1.6 release notes are archival, not supported runtimes. Retain the UnityStubs/IMGUI adapter until alternatives are benchmarked and validated. GitHub is the development and PR engine. ProjectNexus is the selected local validation engine; do not substitute GitHub Actions, run untrusted PR code on a credential-bearing host, claim an in-game PASS without exact evidence, or ship legacy 1.6 binaries.
