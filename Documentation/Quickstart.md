# Insight Canvas quickstart

Insight Canvas 2.1.0 is an opt-in UI toolkit for RimWorld 1.6. It is a separate mod with package ID `lan.insightcanvas`; it does not globally reskin RimWorld and ordinary screens do not need `InsightModel`.

## 1. Add the dependency

Install Insight Canvas alongside the consuming mod. In the consuming mod's `About/About.xml`, declare the package explicitly:

```xml
<modDependencies>
  <li>
    <packageId>lan.insightcanvas</packageId>
    <displayName>Insight Canvas</displayName>
  </li>
</modDependencies>
<loadAfter>
  <li>lan.insightcanvas</li>
</loadAfter>
```

Reference the installed `1.6/Assemblies/InsightCanvas.dll` during compilation. Do not copy that DLL into the consuming mod; ship one framework assembly through the Insight Canvas mod. RimWorld and Unity assemblies are local build inputs and are not redistributed.

## 2. Open a Window

Build a stable-ID tree, put it in a document, and add the public window shell:

```csharp
InsightUiStack root = InsightUi.Column("root",
    InsightUi.Label("title", "Colony settings", InsightUiTextStyle.Title),
    InsightUi.Toggle("show-hints", "Show hints"),
    InsightUi.Button("apply", "Apply"))
    .SetGap(8f).SetPadding(12f);

InsightUiDocument document = new InsightUiDocument("Colony settings", root);
Find.WindowStack.Add(new InsightUiWindow(document));
```

`InsightUiWindow` is resizable and uses the same renderer as an embedded host. The document owns selection, expansion, focus, scroll, theme, effects, and toasts for that window.

## 3. Embed in an existing window

Keep one host for the lifetime of the owning panel. Pass the caller-owned `Rect` from `DoWindowContents`, and close the host from the owner's close path:

```csharp
private readonly InsightUiHost host = new InsightUiHost(BuildDocument());

public void DoWindowContents(UnityEngine.Rect rect)
{
    host.Draw(rect, UnityEngine.Time.deltaTime);
}

public void ClosePanel()
{
    host.PostClose();
}
```

`PostClose()` clears document-owned focus, effects, toasts, popovers, dropdowns, and map-bridge ownership. If a consumer already manages its own lifecycle, it can call `InsightUiRenderer.Draw(rect, document)` directly and still use the same public element tree.

## 4. Compose an existing semantic view

Retained v1 semantic objects can be inserted beside ordinary v2 elements without copying their interaction context:

```csharp
InsightContext context = new InsightContext();
InsightModel model = InsightModel.Create("frontier-analysis");
InsightView view = InsightView.Create().Add(new MyInsightComponent("overview"));
InsightUiElement root = InsightUi.Column("analysis-root",
    InsightUi.Label("summary", "Frontier analysis", InsightUiTextStyle.Heading),
    InsightUi.SemanticView("analysis-view", model, view, context));
InsightUiDocument document = new InsightUiDocument("Frontier analysis", root);
InsightUiHost host = new InsightUiHost(document);
```

`SemanticView` retains `model`, `view`, and `context`. It refreshes the immutable model snapshot during Measure when the model revision changes, uses the cached snapshot during Paint, and defers revisions that arrive during navigation until the next Measure. Theme, high contrast, color-blind adjustment, density, reduced motion, host bounds, delta time, and the enclosing host's overlay ownership are inherited from the document. Call `host.PostClose()` when the owner closes. The existing `InsightCanvasHost`/`InsightWindow` APIs remain available for v1 compatibility.

## Reusable information surfaces

Common RimWorld summaries can stay compositional instead of introducing a data model:

```csharp
InsightUiStatRow reserve = InsightUi.StatRow("reserve", "Stored power", "620 / 1000 Wd")
    .SetSecondary("Workshop reserve");
InsightUiElement summary = InsightUi.Column("summary",
    InsightUi.SectionHeader("storage", "Storage", "Configure stockpile behavior"),
    InsightUi.Callout("power-warning", InsightUiCalloutSeverity.Warning,
        "Insufficient power", "This workbench will stop when stored energy is exhausted.")
        .SetContent(reserve),
    InsightUi.Meter("power", 620f, 1000f).SetLabel("Stored power").SetValueText("620 / 1000 Wd")
).SetGap(8f);
```

`Callout`, `SectionHeader`, `Meter`, and `StatRow` are ordinary composites of the existing primitives. They inherit the document theme, density, contrast, and motion settings and work in either an embedded host or `InsightUiWindow`.

## 5. Bind existing ModSettings

Bindings keep the consumer's settings object authoritative; no second synchronization dictionary is needed:

```csharp
InsightUiToggle hints = InsightUi.Toggle("hints", "Show hints")
    .Bind(() => settings.ShowHints, value => settings.ShowHints = value);
InsightUiSelect density = InsightUi.Select("density", "Density",
    new[] { "Comfortable", "Normal", "Compact" })
    .Bind(() => settings.DensityIndex, value => settings.DensityIndex = value);
```

The getter is read again on later frames, so settings changed by another part of the mod are reflected immediately. See [`Examples/ModSettingsExample.cs`](../Examples/ModSettingsExample.cs) for a complete `ModSettings` example.

## 6. Search and virtualize data

Use `SearchField` for a bound query and `VirtualList` for deterministic fixed-height large collections:

```csharp
InsightUiSearchField search = InsightUi.SearchField("search", placeholder: "Search records")
    .Bind(() => query, value => query = value);
InsightUiVirtualList records = InsightUi.VirtualList("records", 1000, 28f,
    index => InsightUi.Label("record-" + index, "Record " + index));
records.Overscan = 2;
records.CacheLimit = 96;
```

Only the visible range plus overscan is measured, arranged, and painted. `InsightVirtualization.Range` and `ContentHeight` are available for custom adapters.

## 7. Theme and accessibility

Themes and accessibility options are scoped to the document:

```csharp
InsightTheme theme = InsightTheme.Default.Clone();
theme.Selected = new InsightColor(0.30f, 0.62f, 0.72f);
document.Theme = theme;
document.Density = InsightUiDensity.Compact;
document.HighContrast = true;
document.ReducedMotion = true;
document.Invalidate();
```

The default RimWorld+ theme uses warm charcoal surfaces, readable neutral text, muted accents, and code-drawn depth. The renderer restores Unity GUI/Text state after drawing and never mutates the global GUI skin.

## 8. Effects and custom rendering

Use document-owned effects for brief feedback and close them with the document lifecycle:

```csharp
document.Effects.Flash("saved-card");
document.Toasts.Show("Saved", InsightToastSeverity.Success);
```

For a restrained paint-only reveal or display-only context card, use the same ordinary composition API:

```csharp
InsightUiElement details = InsightUi.SlideFade("details", settings.ShowDetails,
    InsightUi.Surface("details-card", InsightUi.Label("copy", "Additional settings")));
InsightUiElement help = InsightUi.HoverCard("help-card",
    InsightUi.Label("help-trigger", "Hover for context"),
    InsightUi.Column("help-content",
        InsightUi.Label("help-title", "Context", InsightUiTextStyle.Heading),
        InsightUi.Label("help-copy", "A short, display-only explanation.")));
```

`SlideFade` keeps the arranged geometry unchanged and uses a short cardinal 4–8 px travel. `HoverCard` waits briefly before opening, allows a small trigger-to-card grace period, clamps to the host Rect, and is cleared by `InsightUiHost.PostClose()` or `InsightUiWindow.PostClose()`. It does not take focus or introduce a general overlay manager.

For a custom preview, use `InsightUi.Custom` and optional renderer capabilities:

```csharp
InsightUiElement preview = InsightUi.Custom("preview", context =>
{
    IInsightUiCustomPainter canvas = context.Painter as IInsightUiCustomPainter;
    if (canvas == null) return;
    canvas.FillRect(context.Bounds, context.Frame.Theme.Surface, context.Frame);
    canvas.Line(context.Bounds.X, context.Bounds.Bottom,
        context.Bounds.Right, context.Bounds.Y, context.Frame.Theme.Selected, 2f, context.Frame);
}, (constraints, frame) => new InsightUiSize(160f, 64f));
```

Custom elements still participate in measure, arrange, paint, tooltips, focus, and document scoping. Keep drawing renderer-neutral and do not change `GUI.skin`.

## Where to go next

- [`Integration.md`](Integration.md) has the complete public API and optional semantic extensions.
- [`Examples/`](../Examples/) contains copy/paste Window, embedded, settings-binding, and custom-drawing examples.
- **Feature Showcase** is available from Insight Canvas mod settings and the development debug action.
