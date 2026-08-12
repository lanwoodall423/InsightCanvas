# Insight Canvas integration

Insight Canvas 2.0.0 is an opt-in, composable UI toolkit for RimWorld 1.6 (`lan.insightcanvas`). Ordinary screens do not require `InsightModel`; they are built from an element tree with stable IDs, a document-owned state store, and a scoped theme. The public tree uses explicit `Measure`, `Arrange`, and `Paint` phases and can be embedded or hosted by a normal RimWorld `Window`.

Start with [`Quickstart.md`](Quickstart.md) for the shortest adoption path. Keep Insight Canvas installed as its own mod, declare `lan.insightcanvas` in the consuming mod's `<modDependencies>`, and reference the installed assembly at compile time without bundling a duplicate DLL.

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

Use `Row`, `Column`, `Wrap`, `Grid`, `Split`, `Scroll`, `Navigation`, and `VirtualList` for responsive composition. `Navigation` renders a side rail above its breakpoint and a wrapped top bar below it. `VirtualList` renders only its bounded visible range for fixed-height rows; set `CacheLimit` for a predictable retained-row bound and use `InsightVirtualization.Range` for custom variable-height adapters. Set `InsightUiSplit.Draggable = true` when the active painter supports `IInsightUiDragPainter`; the ratio is persisted in document state. Leave it false for a static split.

Visual and interactive primitives include `Surface`, `Label`, `Divider`, `Badge`, `Progress`, `Spacer`, `Button`, `IconButton`, `Toggle`, `Slider`, `TextField`, `Select`, `Expander`, `Tabs`, and `Breadcrumbs`. The everyday additions are `Dropdown`, `Popover`, `SearchField`, `Segmented`, `Image`, and document-local `Toast`. Controls use ordinary callbacks, and stateful controls also support small getter/setter bindings when the consumer's model is authoritative:

```csharp
InsightUiToggle autoAssign = InsightUi.Toggle("auto-assign", "Auto assign")
    .Bind(() => settings.AutoAssign, value => settings.AutoAssign = value);
InsightUiSlider volume = InsightUi.Slider("volume", 0.8f, 0f, 1f)
    .Bind(() => settings.Volume, value => settings.Volume = value);
InsightUiTextField label = InsightUi.TextField("label")
    .Bind(() => settings.Label, value => settings.Label = value);
```

The bound getter is read again on subsequent frames, so external changes are reflected without synchronizing a second copy of the value. `Select`, `Expander`, `Tabs`, and `Navigation` expose the same `Bind` pattern for their selected index, expansion, or active ID. Existing constructors and callback-only usage remain valid.

`Dropdown` is the conventional option menu; `Select` remains the compact cycling selector for dense settings. `SearchField` is a text field with placeholder and optional clear affordance, not a search-engine abstraction. `Segmented` owns one selected index for compact mutually exclusive choices. `Popover` composes an existing trigger and transient content; it closes with the document/window lifecycle. These controls use normal measure/arrange/paint and document state, so they work in both embedded hosts and `InsightUiWindow`.

Stable IDs scope selection, active tabs, expansion, text values, and scrolling to one `InsightUiDocument`. Reusable components can add a lightweight identity scope instead of concatenating every descendant key:

```csharp
InsightUiElement settings = InsightUi.Column("settings",
    InsightUi.Scope("audio", InsightUi.Slider("volume", 0.8f, 0f, 1f)),
    InsightUi.Scope("gameplay", InsightUi.Slider("volume", 0.5f, 0f, 1f)));
```

The effective state paths are `audio/volume.value` and `gameplay/volume.value`. Set `document.TrackDuplicateIds = true` during development to expose duplicate stateful or interactive identities through `InsightUiDiagnostics.DuplicateIds` and `DuplicateIdPaths`.

## Custom drawing and icons

`InsightUi.Custom` participates in the regular measure, arrange, and paint phases while keeping renderer details behind a small callback context. A consumer can use optional drawing capabilities when the active renderer supports them:

```csharp
InsightUiElement preview = InsightUi.Custom("research-preview", context =>
{
    IInsightUiCustomPainter canvas = context.Painter as IInsightUiCustomPainter;
    canvas?.FillRect(context.Bounds, context.Frame.Theme.Selected, context.Frame);
    canvas?.Line(context.Bounds.X, context.Bounds.Y, context.Bounds.Right,
        context.Bounds.Bottom, context.Frame.Theme.Focus, 1f, context.Frame);
}, (constraints, frame) => new InsightUiSize(96f, 64f));
```

Use `InsightUiIcon.FromText("!")` for a glyph fallback or `InsightUiIcon.FromTexture(texture, "!")` for a consumer-resolved RimWorld texture. Both `InsightUi.Icon` and the overloaded `InsightUi.IconButton` accept the same model; string-based `IconButton` calls remain source-compatible. Tooltips and accessible descriptions are optional icon metadata.

The optional `IInsightUiIconPainter`, `IInsightUiCustomPainter`, and `IInsightUiFocusPainter` capabilities do not change the existing `IInsightUiPainter` contract, so existing portable or consumer test painters continue to compile. Unity/GUI state remains protected by the renderer's existing outer scope.

## Effects and feedback

Effects are keyed by stable IDs and advance from the document's delta time. They do not change layout unless the consumer changes visibility or sizing explicitly:

```csharp
InsightUiElement details = InsightUi.Fade("details", settings.ShowDetails,
    InsightUi.Surface("details-card", InsightUi.Label("copy", "Additional settings")));

document.Effects.Flash("save-feedback");
document.Toasts.Show("Saved", InsightToastSeverity.Success);
```

`InsightMotionEasing` offers only linear, smooth, ease-out, and approach behavior. A changed target interrupts the current value naturally; reduced motion settles transitions and flashes immediately. `Highlight` is useful for a short success/error emphasis without moving content. Effects, toasts, popovers, and their state are document-owned and are cleared by `InsightUiHost.PostClose()`/`InsightUiWindow.PostClose()`.

## Focus and keyboard input

Each `InsightUiDocument` owns an `InsightUiFocusState`. Stock buttons, toggles, sliders, text fields, selectors, and navigation controls register in paint order; the RimWorld adapter supports Tab/Shift+Tab traversal and Enter/Space activation where the control has an activation action. Focus rings use the document theme's `Focus` token. Text editing suppresses document-level activation handling. Consumers can inspect or request focus through `document.Focus`; Escape remains owned by the RimWorld window shell.

`InsightUiDiagnostics` reports frame, measure, arrange, visible-element, invalidation, render-error, and optional duplicate-ID counters. `InsightUiFrame` carries the document theme, density, reduced-motion setting, state store, diagnostics, focus state, delta time, text measurement service, and the current reusable-component scope.

## Themes and accessibility

`InsightTheme.Default` is a warm-charcoal RimWorld+ palette with neutral text, muted accents, shallow elevation, and readable spacing. `Surface` honors background, elevation/shadow, border color/width, clipping, and theme typography multipliers. RimWorld's box drawing remains square because rounded corners are not a cheap, portable IMGUI primitive; `CornerRadius` is retained as a theme/style token for capable custom painters rather than silently creating per-frame textures. `PanelTexturePath` and `BorderTexturePath` remain resource hints for custom painters; the default adapter uses code-drawn surfaces and does not allocate textures per repaint. Clone it before changing tokens:

```csharp
InsightTheme scoped = InsightTheme.Default.Clone();
scoped.Selected = new InsightColor(0.30f, 0.62f, 0.72f);
document.Theme = scoped;
document.Density = InsightUiDensity.Compact;
document.HighContrast = true;
document.ReducedMotion = true;
document.Invalidate();
```

The renderer applies accessibility adjustments at the document boundary, caches the adjusted theme until its inputs change, uses typography multipliers for text measurement/drawing, and restores Unity GUI/Text state after drawing. No required artwork or Harmony dependency is involved.

## Optional semantic extensions

Mods that have knowledge or analysis data can still use `InsightModel`, `InsightContext`, `InsightGraphLayout`, `InsightTimelineMath`, explanation types, serialization, and `InsightMapBridge`. These are optional data/widget extensions; a normal menu does not need to create a semantic model. The retained semantic `InsightWindow` and `InsightCanvasHost` are for existing advanced consumers while new integrations should prefer `InsightUiDocument`, `InsightUiHost`, and `InsightUiWindow`.

`InsightMapBridge` can create transient focus, flash, heatmap, outline, radius, and path actions. Callers should invoke map actions from callbacks, not from repaint-time data collection. `InsightUiHost.PostClose()` and `InsightUiWindow.PostClose()` clear overlays owned by that host.

## Serialization contract

`InsightModelSerialization` writes deterministic XML schema version 2 for the optional semantic model. Entity and manual-position maps use ordinal ID ordering; event, badge, metric, action, history, and explanation-operation list order is preserved. Runtime delegates, live source objects, textures, map references, and callbacks are not serialized. Loaded actions remain disabled until a consumer rebinds a callback through the public API. Call `model.Validate()` before publishing or saving.

## Engine limitations

RimWorld uses an outer IMGUI repaint loop. Insight Canvas keeps stable model work outside repaint through snapshots and caches, measures only the active responsive branch where possible, and bounds virtualized collections and graph work. It does not globally reskin the game and does not query maps or worlds from ordinary element paint code.
