# Insight Canvas integration

Insight Canvas v2 is an opt-in, composable UI toolkit. Ordinary screens do not require `InsightModel`; they are built from an element tree with stable IDs, a document-owned state store, and a scoped theme. The public tree uses explicit `Measure`, `Arrange`, and `Paint` phases and can be embedded or hosted by a normal RimWorld `Window`.

## Embedded panel

Use `InsightUiHost` when another mod owns the Window and supplies the drawing rectangle:

```csharp
using InsightCanvas;
using UnityEngine;
using Verse;

public sealed class ResearchPanel
{
    private readonly InsightUiHost host;

    public ResearchPanel()
    {
        InsightUiElement root = InsightUi.Column("research-root",
            InsightUi.Breadcrumbs("crumbs", "Colony", "Research"),
            InsightUi.Label("title", "Research brief", InsightUiTextStyle.Heading),
            InsightUi.Surface("status-card", InsightUi.Column("status-body",
                InsightUi.Badge("status", "Ready", InsightTheme.Default.Positive),
                InsightUi.Progress("confidence", 0.72f, InsightTheme.Default.Selected))),
            InsightUi.Row("actions",
                InsightUi.Button("refresh", "Refresh", Refresh),
                InsightUi.IconButton("help", "?", ShowHelp))
        ).SetGap(8f).SetPadding(12f);

        host = new InsightUiHost(new InsightUiDocument("Research panel", root));
    }

    public void Draw(Rect rect)
    {
        host.Draw(rect, Time.deltaTime);
    }

    public void Close()
    {
        host.PostClose();
    }

    private void Refresh() { }
    private void ShowHelp() { }
}
```

The host owns the document lifecycle. Call `PostClose()` from the consumer's close path so owner-scoped map overlays are cleared.

## Window shell

Use the same document in a complete resizable RimWorld window:

```csharp
InsightUiElement root = InsightUi.Column("settings-root",
    InsightUi.Label("title", "Colony settings", InsightUiTextStyle.Title),
    InsightUi.Toggle("show-hints", "Show hints"),
    InsightUi.Select("density", "Density", new[] { "Comfortable", "Normal", "Compact" }),
    InsightUi.Expander("advanced", "Advanced", InsightUi.Label("advanced-copy", "More settings here."))
).SetGap(10f).SetPadding(12f);

InsightUiDocument document = new InsightUiDocument("Settings", root)
{
    Density = InsightUiDensity.Normal,
    HighContrast = false,
    ReducedMotion = false
};
Find.WindowStack.Add(new InsightUiWindow("Settings", document));
```

`InsightUiWindow` uses the same `InsightUiHost` and renderer as embedding. It does not change `GUI.skin`, vanilla windows, or third-party windows globally.

## Composition and state

Use `Row`, `Column`, `Wrap`, `Grid`, `Split`, `Scroll`, `Navigation`, and `VirtualList` for responsive composition. `Navigation` renders a side rail above its breakpoint and a wrapped top bar below it. `VirtualList` renders only its bounded visible range for fixed-height rows; use `InsightVirtualization.Range` for custom variable-height adapters.

Visual and interactive primitives include `Surface`, `Label`, `Divider`, `Badge`, `Progress`, `Spacer`, `Button`, `IconButton`, `Toggle`, `Slider`, `TextField`, `Select`, `Expander`, `Tabs`, and `Breadcrumbs`. Controls use ordinary callbacks. Stable IDs scope selection, active tabs, expansion, text values, and scrolling to one `InsightUiDocument`.

`InsightUiDiagnostics` reports frame, measure, arrange, visible-element, invalidation, and render-error counters. `InsightUiFrame` carries the document theme, density, reduced-motion setting, state store, diagnostics, delta time, and text measurement service.

## Themes and accessibility

`InsightTheme.Default` is a warm-charcoal RimWorld+ palette with neutral text, muted accents, shallow elevation, and readable spacing. Clone it before changing tokens:

```csharp
InsightTheme scoped = InsightTheme.Default.Clone();
scoped.Selected = new InsightColor(0.30f, 0.62f, 0.72f);
document.Theme = scoped;
document.Density = InsightUiDensity.Compact;
document.HighContrast = true;
document.ReducedMotion = true;
document.Invalidate();
```

The renderer applies accessibility adjustments at the document boundary and restores Unity GUI/Text state after drawing. No required artwork or Harmony dependency is involved.

## Optional semantic extensions

Mods that have knowledge or analysis data can still use `InsightModel`, `InsightContext`, `InsightGraphLayout`, `InsightTimelineMath`, explanation types, serialization, and `InsightMapBridge`. These are optional data/widget extensions; a normal menu does not need to create a semantic model. The retained semantic `InsightWindow` and `InsightCanvasHost` are for existing advanced consumers while new integrations should prefer `InsightUiDocument`, `InsightUiHost`, and `InsightUiWindow`.

`InsightMapBridge` can create transient focus, flash, heatmap, outline, radius, and path actions. Callers should invoke map actions from callbacks, not from repaint-time data collection. `InsightUiHost.PostClose()` and `InsightUiWindow.PostClose()` clear overlays owned by that host.

## Serialization contract

`InsightModelSerialization` writes deterministic XML schema version 2 for the optional semantic model. Entity and manual-position maps use ordinal ID ordering; event, badge, metric, action, history, and explanation-operation list order is preserved. Runtime delegates, live source objects, textures, map references, and callbacks are not serialized. Loaded actions remain disabled until a consumer rebinds a callback through the public API. Call `model.Validate()` before publishing or saving.

## Engine limitations

RimWorld uses an outer IMGUI repaint loop. Insight Canvas keeps stable model work outside repaint through snapshots and caches, measures only the active responsive branch where possible, and bounds virtualized collections and graph work. It does not globally reskin the game and does not query maps or worlds from ordinary element paint code.
