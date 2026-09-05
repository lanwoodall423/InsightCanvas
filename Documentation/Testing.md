# Testing and manual exercise

## Portable check

Run the game-independent harness from the repository root:

```sh
dotnet run --project Tests/InsightCanvas.CoreTests.csproj --configuration Release --nologo
```

The harness covers model validation and serialization, responsive layout math, flex/wrap/grid/split behavior, document state isolation and root replacement, scoped themes and accessibility, deterministic graph/timeline behavior, virtualization bounds and bounded cache eviction, motion/effects easing and reduced-motion progression, SlideFade interruption/settling and nested translation restoration, HoverCard delay/grace/edge placement/cleanup, toast expiry, showcase navigation breakpoints, scoped showcase settings, deterministic showcase records, controlled versus uncontrolled Toggle/Slider/TextField/Select bindings, hierarchical `InsightUi.Scope` state paths, duplicate-ID diagnostics, custom drawing capability dispatch, icon fallback/metadata, fade participation in layout, pure focus traversal, and the renderer-neutral semantic lifecycle. Semantic coverage verifies retained contexts, independent shared-model sources, revision-keyed snapshot caching, deferred refresh during navigation, resize invalidation, accessibility/density/reduced-motion/bounds/delta/owner propagation, bounded contained errors, and no per-frame rebuild.

## Portable release checks

The repository-level release checks run without RimWorld and are enforced by CI:

```powershell
pwsh -NoProfile -File Tools/release_checks.ps1 -Command api
pwsh -NoProfile -File Tools/release_checks.ps1 -Command version
pwsh -NoProfile -File Tools/release_checks.ps1 -Command package
```

`PublicAPI.Shipped.txt` is the committed semantic-versioning baseline. Compatible additions
must first be listed in `PublicAPI.Unshipped.txt`; removals, signature changes, visibility
changes, and enum-member changes fail the API check. Update the shipped baseline only for an
acknowledged major-version release.

The independent consumer sample compiles against the released-style framework DLL and local RimWorld
1.6 managed references without copying framework or proprietary game assemblies:

```powershell
dotnet build Examples/MinimalConsumer/InsightCanvas.MinimalConsumer.csproj --configuration Release `
  /p:RimWorldDir="$env:RIMWORLD_ROOT"
```

## RimWorld build

With a local RimWorld 1.6 installation, run:

```powershell
dotnet build Source/InsightCanvas.csproj --configuration Release --nologo /p:RimWorldDir="$env:RIMWORLD_ROOT"
```

The build writes `1.6/Assemblies/InsightCanvas.dll` and `.xml`. `git diff --check` is useful before packaging.

For the checked-in runtime freshness gate, the script builds Release into an isolated temporary
output directory and compares SHA-256 hashes for both `InsightCanvas.dll` and `InsightCanvas.xml`
against `1.6/Assemblies`. Deterministic builds make this byte-for-byte comparison intentional:

```powershell
pwsh -NoProfile -File Tools/release_checks.ps1 -Command freshness `
  -RimWorldDir "$env:RIMWORLD_ROOT"
```

The canonical package command stages only `About/**`, `1.6/**`, and an optional `LoadFolders.xml`,
rejects proprietary or unexpected DLLs, writes a fixed-timestamp ZIP, and emits its `.sha256`
manifest under `artifacts/`. The package output is ignored by Git.

## RimTest validation

RimTest is the authoritative development workflow for the mod. From the repository root:

```powershell
& (Join-Path $env:RIMTEST_ROOT 'rimtest.cmd') doctor --json
& (Join-Path $env:RIMTEST_ROOT 'rimtest.cmd') affected --run --json
```

Run `doctor` only when readiness is unknown. `affected` uses RimContext to select the catalog test and
delegates its registered recipe to DevBridge2. DevBridge2 owns RimWorld's lifecycle, profiles,
generations, and ModsConfig state; do not launch or stop RimWorld manually or edit ModsConfig
directly.

The `insightcanvas-in-game-smoke` catalog test is the intended artifact-freshness anchor for the
framework. It maps the principal mod, showcase, host, renderer, semantic bridge, theme, map bridge,
document, and virtualization types to the RimTest-owned workflow so relevant source changes select
the runtime check directly; the configured `smoke` suite remains the conservative fallback.

The catalog entry maps to the project-scoped DevBridge2 v2 recipe
`insightcanvas-in-game-suite`, whose `projects` list contains the canonical `insight-canvas` alias.
The recipe requires an authenticated RimBridgeServer companion operation, so DevBridge2 remains the
owner of lifecycle, profile, generation, readiness, lease, and operation routing while the
development-only `InsightCanvas.BridgeTools` assembly owns the Feature Showcase assertions.

The companion renders the real `InsightUiWindow`, visits all ten pages, checks wide/narrow navigation
and finite geometry, exercises public controls and document-scoped state, validates the retained
semantic-view API, and checks render/duplicate-ID/virtualization diagnostics before closing the window
and clearing owner-scoped map overlays. It returns a bounded report through the recipe; no GameComponent
test runner, request file, direct RimWorld launch, or second lifecycle owner is used.

Build the development-only companion before a local runtime run:

```powershell
dotnet build DevTools/BridgeTools/InsightCanvas.BridgeTools.csproj --configuration Release `
  /p:RimWorldRoot="$env:RIMWORLD_ROOT" `
  /p:RimBridgeSdkPath="$env:RIMWORLD_ROOT\Mods\RimBridgeServer\1.6\Assemblies\RimBridgeServer.Sdk.dll"
```

The companion is written to the deployed Insight Canvas mod's `1.6\BridgeTools` load folder and is
not part of the release package. The authoritative current-source command remains
`rimtest affected --run --json`; inspect its `artifactFreshness` object before treating a passing
runtime result as proof for the edited source.

## Feature Showcase checklist

1. Open **Mod settings > Insight Canvas > Open Feature Showcase** or the development-mode **Insight Canvas > Open Feature Showcase** action.
2. Resize the ordinary Window wide and narrow. Confirm the ten-page side rail becomes a compact wrapped top navigation without clipped primary actions.
3. On Overview, expand the compact inspector and trigger the map action. With no playable map, confirm the useful empty-state message; with a map, confirm the center flash action.
4. On Foundations, inspect typography, spacing/density notes, surfaces, separators, badges, and semantic status colors.
5. On Layout, drag width simulation, inspect wrapping, adaptive cards, split panes, and the reachable scroll sample.
6. On Controls, exercise toggles, the selector, slider, text field, selected/disabled/warning/destructive buttons, icon action, expander, and the brief display-only HoverCard context treatment. Confirm status text changes.
7. On Navigation and Workspaces, switch tabs, use toolbar actions, breadcrumbs, the inspector pane, and comparison layout.
8. On Data Display, filter the deterministic virtualized list and select two records. Confirm the comparison inspector and empty search state.
 9. On Motion and Feedback, reveal progress states, expand the reveal panel, select the milestone, toggle reduced motion live, and press **Reveal next state** to dogfood the paint-only `SlideFade` detail card.
10. On Themes and Accessibility, switch RimWorld+, Field Notes, and Night Watch, change density, high contrast, and reduced motion. Open another consumer to confirm settings remain document-scoped.
11. On Advanced Widgets, inspect the optional graph, timeline, explanation, and map-link cards; the surrounding UI remains usable without semantic data.
12. On Diagnostics, confirm frame/render status, visible element count, layout passes, invalidations, selected page, breakpoint mode, theme/density, and captured error state.

## Optional advanced extension checks

Consumers of semantic extensions should additionally verify snapshot revisioning, deterministic graph fit, timeline clustering, explanation disclosure, serialization omission diagnostics, stable duplicate IDs, replacement of model/view/context sources, root replacement with retained document state, host close/reopen, shared models with independent contexts, no nested owner scope, and owner-scoped map overlay cleanup. These checks are not required to create or draw ordinary composable UI.
