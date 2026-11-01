# Waylith / RimWorld 1.7 opportunity roadmap

**Project policy:** RimWorld 1.7 only; GitHub development and PR review, Local / ProjectNexus validation. These are candidates for later evaluation, not automatic authorizations or validated features.

## First: the compatibility gate

- Compile Insight Canvas on .NET 10 against local 1.7 Assembly-CSharp.dll, Waylith.dll and UnityStubs.dll. Preserve the existing IMGUI renderer, because the official primer explicitly permits and recommends the compatibility layer for now.
- Rebuild the independent consumer and the 1.7 test companion when RimBridgeServer is ready. Test all ten showcase pages and runtime cleanup with an immutable package under RimLiaison.
- Verify package freshness and version consistency. No 1.6 binary, game-version declaration or fallthrough package is accepted. Keep unpublished until exact ProjectNexus evidence is passed.

## Potential improvements (after 1.7 acceptance)

| ID | Priority | Idea | Benefit | Proof needed |
| --- | --- | --- | --- | --- |
| WAY-01 | High | Waylith Frame Profiler baselines | Identify costly Measure/Arrange/Paint, virtualization, effects and semantic rendering using frame-time, allocation and GC data | Compare captured, repeatable showcase scenarios; account for profiler overhead |
| WAY-02 | High | -Headless and -NoWindow test profiles | Faster non-graphics tests and unattended graphical snapshots respectively | Integrate under sole RimLiaison runtime authority, with safe capture and cleanup |
| WAY-03 | High | -mod and -ColdBoot regression profiles | Exact mod closure without altering ModsConfig; reproducible cold-asset performance | Prove dependency closure, warm/cold comparison and profile restoration |
| WAY-04 | Medium | Waylith.Jobs for pure layout/graph data calculations | Parallelize independent off-main-thread computations | No GUI, live game or UnityStub objects on worker threads; measure and test deterministic results |
| WAY-05 | Medium | Optional GLSL effects and ShaderDef/MaterialDef support | New Vulkan-backed visuals and hot-reload experimentation | Feature flag, fallback renderer, GPU/VRAM/accessibility tests and shader error handling |
| WAY-06 | Later | Asset-pack cache and authoring workflow | Explore -BakeModAssetPacks, incremental assets and startup | Compare the exact 1.7 package under RimLiaison; no legacy Unity assumptions |

No native Waylith rewrite is proposed without benchmark evidence. Do not run untrusted PR executables on the Windows host, redistribute game DLLs, automatically broaden trust, or replace ProjectNexus validation with GitHub Actions.

## Engine references

- https://ludeon.com/blog/2026/10/announcing-the-waylith-engine-and-update-1-7/
- https://docs.google.com/document/d/e/2PACX-1vSykw5_DYaaw42nEznI9zmBZVT_Vgg2bxYq_v2XWvAsHC3QSKFuDK2OyiQ5WC9jvVRUcrjpFt7xLLF5/pub
- https://rimworldwiki.com/wiki/Modding_Tutorials/RimWorld_1.7_Mod_Updates
