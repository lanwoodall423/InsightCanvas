# Insight Canvas 2.2.0

**Status: draft, not released, not locally validated.** These notes describe the proposed RimWorld 1.7 / Waylith migration and must not be presented as installation instructions for a completed release.

## Scope and compatibility

- **RimWorld 1.7 only.** Compatibility declarations and versioned package folders for 1.6 are removed. Previous 2.1.1 release notes remain as historical records.
- Updated SDK-style projects target `net10.0` and reference RimWorld 1.7 `Assembly-CSharp.dll`, `Waylith.dll`, and `UnityStubs.dll`. No proprietary DLLs are distributed.
- The established IMGUI renderer remains intact through Waylith's Unity compatibility layer; no unverified native rendering rewrite is performed.
- The mod package ID remains `lan.insightcanvas`. The documented C# public API is intended to remain source compatible, subject to independent consumer compilation.
- Compiled framework version: **2.2.0.0** (candidate; not yet built).

## Required before publication

A newly compiled 1.7 DLL and XML documentation, .NET 10 SDK and game reference confirmation, portable and independent consumer builds, release packaging/freshness checks, and the exact ProjectNexus local validation suite must pass. The RimLiaison/DevBridge2 ten-page in-game test requires independently verified Waylith-ready RimBridgeServer. The migration currently has **no PASS claim** for those gates, and the checked-in 1.6 DLL has intentionally been deleted without replacement.

Development happens through the GitHub PR; validation is owned by Local / ProjectNexus (not GitHub Actions). PR-sourced execution on the credential-bearing Windows host remains prohibited until an approved isolation boundary exists. Review acceptance, source integration, installation, and publication are separate decisions.

## Potential engine improvements

See [WaylithRoadmap.md](WaylithRoadmap.md) for candidates: Frame Profiler baselines, headless/hidden tests under one runtime authority, deterministic mod/cold-boot profiles, pure-data jobs, optional Vulkan/GLSL visuals, and asset baking. These are proposals, not implemented features.
