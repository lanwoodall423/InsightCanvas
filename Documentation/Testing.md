# Testing And Manual Exercise

## Portable CI-equivalent check

From the repository root, run this single command:

```sh
dotnet run --project Tests/InsightCanvas.CoreTests.csproj --configuration Release
```

`dotnet run` restores, builds, and runs the executable harness. It returns a nonzero exit code on restore, build, or test failure. The standalone harness covers model endpoint and ID validation, stable ids, responsive layout math, shared selection and hover state, explanation calculations, theme XML parsing, deterministic graph layout, timeline clustering/zoom, full snapshot serialization, legacy XML loading, runtime omission diagnostics, configured action intent, safe callback rebinding, equivalent insertion-order serialization, and deterministic output.

## Optional local mod verification

The real mod assembly is deliberately outside CI because RimWorld’s managed assemblies are proprietary and are not available from this repository. Set `RimWorldDir` as described in the README, then run:

```sh
dotnet build Source/InsightCanvas.csproj --configuration Release --nologo
dotnet format Source/InsightCanvas.csproj --verify-no-changes --no-restore
```

The build targets RimWorld 1.6 and writes `1.6/Assemblies/InsightCanvas.dll` and `.xml`. The portable project remains usable without a RimWorld installation. For the complete local review, also run `dotnet format Tests/InsightCanvas.CoreTests.csproj --verify-no-changes --no-restore` and `git diff --check`.

Before release, require both a visible green GitHub Actions run for the exact commit and an in-game RimWorld 1.6 smoke test of the installed package. CI cannot cover game DLL compatibility, RimWorld window lifecycle, map overlays, or actual Unity rendering.

## Laboratory checklist

1. Open the laboratory from mod settings while playing and resize it from a wide desktop window to a narrow window.
2. Hover and select a card; confirm the constellation node, explanation panel, and event river use the same entity.
3. Use the graph fit, focus, middle-drag, and wheel zoom controls. Inspect low-zoom cluster aggregation and high-zoom relation labels.
4. Change disclosure from Unknown through Mastered. Exact values, history, causal factors, and labels should reveal progressively; unknown states retain symbols and text.
5. Open a card action and preview a map link both with and without a current map. Close the window and confirm temporary overlays do not remain.
6. Open Laboratory Tools to inspect theme tokens, parse an XML theme, simulate resolution/UI scale, inspect interaction state, and open graph/timeline stress views.
7. Check empty models, missing relation endpoints, cyclic relations, disconnected nodes, unknown metrics, oversized datasets, and rapidly changing model revisions.

RimWorld's outer IMGUI loop continues to repaint a window even when it is idle. Stable model data is nevertheless snapshot- and revision-cached; graph relaxation and timeline aggregation are budgeted, and no map/world query is made from repaint code.
