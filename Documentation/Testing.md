# Testing And Manual Exercise

## Automated checks

From the mod directory:

```text
dotnet build Source\InsightCanvas.csproj -c Release --nologo
dotnet run --project Tests\InsightCanvas.CoreTests.csproj -c Release
```

The standalone harness covers model endpoint validation, stable ids, responsive layout math, shared selection and hover state, explanation calculations, theme XML parsing, deterministic graph layout, timeline clustering/zoom, and snapshot serialization.

## Laboratory checklist

1. Open the laboratory from mod settings while playing and resize it from a wide desktop window to a narrow window.
2. Hover and select a card; confirm the constellation node, explanation panel, and event river use the same entity.
3. Use the graph fit, focus, middle-drag, and wheel zoom controls. Inspect low-zoom cluster aggregation and high-zoom relation labels.
4. Change disclosure from Unknown through Mastered. Exact values, history, causal factors, and labels should reveal progressively; unknown states retain symbols and text.
5. Open a card action and preview a map link both with and without a current map. Close the window and confirm temporary overlays do not remain.
6. Open Laboratory Tools to inspect theme tokens, parse an XML theme, simulate resolution/UI scale, inspect interaction state, and open graph/timeline stress views.
7. Check empty models, missing relation endpoints, cyclic relations, disconnected nodes, unknown metrics, oversized datasets, and rapidly changing model revisions.

RimWorld's outer IMGUI loop continues to repaint a window even when it is idle. Stable model data is nevertheless snapshot- and revision-cached; graph relaxation and timeline aggregation are budgeted, and no map/world query is made from repaint code.
