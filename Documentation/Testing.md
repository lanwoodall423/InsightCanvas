# Testing and manual exercise

## Portable check

Run the game-independent harness from the repository root:

```sh
dotnet run --project Tests/InsightCanvas.CoreTests.csproj --configuration Release --nologo
```

The harness covers model validation and serialization, responsive layout math, flex/wrap/grid/split behavior, document state isolation and root replacement, scoped themes and accessibility, deterministic graph/timeline behavior, virtualization bounds and bounded cache eviction, motion/effects easing and reduced-motion progression, SlideFade interruption/settling and nested translation restoration, HoverCard delay/grace/edge placement/cleanup, toast expiry, showcase navigation breakpoints, scoped showcase settings, deterministic showcase records, controlled versus uncontrolled Toggle/Slider/TextField/Select bindings, hierarchical `InsightUi.Scope` state paths, duplicate-ID diagnostics, custom drawing capability dispatch, icon fallback/metadata, fade participation in layout, pure focus traversal, and the renderer-neutral semantic lifecycle. Semantic coverage verifies retained contexts, independent shared-model sources, revision-keyed snapshot caching, deferred refresh during navigation, resize invalidation, accessibility/density/reduced-motion/bounds/delta/owner propagation, bounded contained errors, and no per-frame rebuild.

## RimWorld build

With a local RimWorld 1.6 installation, run:

```powershell
dotnet build Source/InsightCanvas.csproj --configuration Release --nologo /p:RimWorldDir="C:\Games\Steam\steamapps\common\RimWorld"
```

The build writes `1.6/Assemblies/InsightCanvas.dll` and `.xml`. `git diff --check` is useful before packaging. DevBridge2 is not part of the test implementation.

## Automatic in-game suite

`Source/InsightCanvasAutoTest.cs` owns the automatic in-game checks. It activates only when a DevBridge2 quicktest launch supplies `DEVBRIDGE_ROOT`; DevBridge2 only launches/restarts RimWorld, reports readiness, and coordinates leases. The mod waits for a playable map, validates the optional deterministic semantic sample and map flash, checks the Unity painter's translation and pointer capability wiring, checks flex and virtualization math, checks per-document state isolation, creates the public-API Feature Showcase, verifies all ten pages, switches through wide side navigation and narrow compact navigation, waits for rendered frames, and verifies layout, visible elements, and render-error counters, including the dogfooded SlideFade and HoverCard.

The suite also exercises the public `InsightUi.SemanticView` bridge with ordinary elements, shared model sources,
independent contexts, retained snapshot reuse, deferred replacement, root replacement, and document-state retention.
The result is written by the mod to `DevBridge2/Runtime/insightcanvas-autotest.json`:

```text
DevBridge.cmd restart
DevBridge.cmd test begin
# wait for the result file to report PASS or FAIL
DevBridge.cmd test end <lease-id printed by test begin>
```

Never add test execution logic to DevBridge2 and never launch or stop RimWorld directly for this workflow. The automatic suite intentionally does not synthesize Unity `Event.current`; it verifies rendered frames and renderer diagnostics. Manually smoke-test Tab/Shift+Tab, Enter/Space activation, focus rings, a real consumer `Texture2D`, and a custom painter callback in RimWorld when changing those adapter paths. Text editing must retain input ownership, and Escape remains RimWorld Window behavior.

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
