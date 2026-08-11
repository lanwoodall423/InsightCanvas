# Insight Canvas Integration

## Composable UI v2

The general-purpose API is deliberately independent of `InsightModel`. A consumer creates an ordinary element tree and supplies stable IDs for elements whose state should persist:

```csharp
InsightUiElement root = InsightUi.Column("research-screen",
    InsightUi.Breadcrumbs("crumbs", "Colony", "Research"),
    InsightUi.Row("toolbar",
        InsightUi.Button("refresh", "Refresh", Refresh),
        InsightUi.IconButton("help", "?", ShowHelp).SetTooltip("Explain this screen")),
    InsightUi.Split("body",
        InsightUi.Surface("navigation", InsightUi.Tabs("sections")
            .Add("overview", "Overview", BuildOverview())
            .Add("history", "History", BuildHistory())),
        InsightUi.Scroll("details", BuildDetails()), 0.28f).SetFlex(1f))
    .SetGap(10f)
    .SetPadding(12f);

InsightUiDocument document = new InsightUiDocument("Research screen", root)
{
    Density = InsightUiDensity.Normal,
    HighContrast = false,
    ReducedMotion = false
};
Find.WindowStack.Add(new InsightUiWindow(document));
```

The same document can be embedded in a caller-owned `Rect`:

```csharp
private readonly InsightUiHost host = new InsightUiHost(document);

public override void DoWindowContents(Rect rect)
{
    host.Draw(rect, Time.deltaTime);
}

public override void PostClose()
{
    host.PostClose();
    base.PostClose();
}
```

`InsightUiElement` has explicit `Measure`, `Arrange`, and `Paint` phases. `InsightUi.Row`, `Column`, `Wrap`, `Grid`, `Split`, `Scroll`, and `VirtualList` provide layout; `Surface`, `Badge`, `Progress`, `Label`, `Button`, `IconButton`, `Toggle`, `Slider`, `TextField`, `Tabs`, and `Breadcrumbs` provide composable visual and interaction primitives. `InsightUiStateStore` is owned by the document, so the same stable ID can be reused in separate windows without leaking selection or scroll state. `InsightUiDiagnostics` reports frames, measure/arrange work, visible elements, invalidations, and render failures.

`InsightTheme` is cloned and accessibility-adjusted at the document boundary. The renderer scopes GUI state and restores color, enabled state, text settings, matrix, and clipping state after every draw. No global `GUI.skin` or vanilla window is changed. For long collections, use `InsightVirtualization.Range` for custom renderers or `InsightUi.VirtualList` for fixed-height rows.

The Feature Showcase is an example consumer of this API, not a privileged framework path. The semantic model, graph, explanation, event, timeline, and map-link types below remain available as optional advanced extensions; ordinary menus should not depend on them.

## Minimal model

The public model is a fluent collector. IDs are owned by the dependent mod and should remain stable for the lifetime of a source object.

```csharp
using InsightCanvas;
using Verse;

InsightEntity fish = new InsightEntity("species:trout", "Silver Trout", "Migratory fish", "Species");
InsightEntity river = new InsightEntity("habitat:river", "Ashwater River", category: "Habitat");
InsightModel model = InsightModel.Create("Example.Ecology")
    .Entity(fish)
    .Entity(river)
    .Relation(river.Id, fish.Id, "contains", 1f, directed: false)
    .Metric(fish.Id, "Population", new InsightMetric("Population", 38f, new InsightRange(20f, 60f)))
    .Explanation(fish.Id, Explain.Value("Catch chance", 0.73f)
        .Base(0.5f)
        .Factor("Knowledge", 1.24f)
        .Factor("Lure mismatch", 0.70f)
        .Clamp("Population scarcity", 0.12f, 0.73f));

Find.WindowStack.Add(new InsightWindow(
    model,
    InsightView.Create()
        .Add(new InsightCardGrid())
        .Add(new InsightConstellation())
        .Add(new InsightExplanationPanel())
        .Add(new InsightEventRiver())));
```

`InsightWindow` takes a snapshot when the model revision changes. A producer can collect or replace data outside repaint and then call `model.Metric`, `model.Relation`, or `model.Event`; no map or world query should happen from a component's `Draw` method.

For a main tab or another host window that owns its own frame chrome, use `InsightCanvasHost` instead of `InsightWindow`. Construct it with the model, view, and optional context; call `Draw(rect, deltaTime)` from `DoWindowContents`, and call `PostClose()` when the host closes.

For a host such as a RimWorld `MainTabWindow`, use `InsightCanvasHost` with the same model, view, and context. It owns the same immutable snapshot boundary and exposes `Draw(Rect)`, `Diagnostics`, `Snapshot`, and `PostClose()`. Long-lived producers can call `InsightModel.Clear()` outside repaint before publishing the next complete collection pass; the host keeps the shared `InsightContext` and selection intact.

## Serialization contract

`InsightModelSerialization` has two compatible entry points:

```csharp
InsightModelSerializationReport saved = InsightModelSerialization.SerializeWithDiagnostics(model.Snapshot());
string xml = saved.Xml;
foreach (string warning in saved.Warnings) Log.Warning(warning);

InsightModelSerializationReport loaded = InsightModelSerialization.DeserializeWithDiagnostics(xml);
InsightModel restored = loaded.Model;
```

The current XML has `schemaVersion="2"` and is emitted deterministically. Entities and manual positions sort by ordinal entity ID; owner-keyed dictionaries and unordered relations use stable ordinal ordering. Badge, metric, action, history, explanation-operation, and event list order is preserved because those sequences may be display or interpretation order. The reader accepts the original unversioned format as schema 1. The round-trippable boundary is pure model/display data: entity text, stable IDs, badges, `ManualPosition`, `SourceId`, `IconId`, relations, metrics and history, events and their entity/map-link IDs, explanations and their entity owners, and action IDs/labels/tooltips/close metadata.

Runtime-only values are intentionally not reconstructed: `InsightEntity.Source`, `InsightEntity.Icon`, map or game object references, `InsightAction.Callback`, and live source/delegate state. Serialization reports warnings when those values are omitted. `InsightAction.ConfiguredEnabled` preserves the integration’s enabled intent, while `Enabled` means currently executable. Deserialized actions have `Callback == null` and `Enabled == false`, so loading cannot create an apparently usable action with no implementation. Rebind an action already in a model with `model.RebindAction(entityId, actionId, callback)`, or use `loadedAction.Rebind(callback)` while rebuilding a new model; do not add a replacement beside the same-ID action. These APIs preserve configured intent and require a non-null callback before invocation. Rebind live sources, textures, and map links in the integration after loading; `InsightEvent.MapLinkId` remains a stable pure-data key only.

Call `model.Validate()` before publishing or saving a model. Validation accumulates errors and warnings for empty or duplicate IDs, missing owners/endpoints, dangling manual positions, invalid numeric/display metadata, and configured-enabled actions without callbacks. Each diagnostic includes the collection, relevant ID, and reason. Warnings are non-fatal display/runtime concerns; errors mean the model violates its documented reference or structural contract.

The portable CI workflow verifies the .NET 8 model, serialization, validation, layout, timeline, and other game-independent behavior on pushes and pull requests. It does not compile or execute the RimWorld-facing assembly. A release still requires a visible green CI run and a local RimWorld 1.6 smoke test with the built package.

## Shared context

All stock components receive the same `InsightContext`. They use `Select`, `Hover`, `Focus`, `SetFilter`, and `SetTimeRange`. Implementations should call `DisclosureFor` before displaying exact values. `IDisclosureProvider` is deliberately generic; `TieredDisclosureProvider` is only a convenient preview adapter and does not define a required knowledge system.

## Map links

Use `InsightMapBridge.For(Thing)`, `For(Pawn)`, `ForCell`, `ForCells`, `For(Zone)`, `For(Area)`, `For(WorldObject)`, or `ForWorldTile`. `InsightMapBridge.Focus` selects/focuses a live target when possible. `Flash` and `Heatmap` register transient map drawing and expire automatically; `InsightWindow.PostClose` clears temporary overlays. Destroyed targets are ignored safely.

For timeline links, call `InsightMapBridge.RegisterLink("stable-link-id", reference)` and put that id in `InsightEvent.MapLinkId`; unregister it when the producer stops publishing the event.

## Themes and accessibility

`InsightTheme.Default` supplies semantic tokens. `InsightTheme.FromXml` accepts optional `<color name="selected" value="#..."/>`, spacing, corner radius, and panel/border texture paths. Use `WithAccessibility(highContrast, mode)` before passing a theme to a custom renderer. Stock views also pair status color with text, symbols, borders, opacity, and labels so color is never the only signal.

`InsightView` retains its original four-component coordinated arrangement and supports additional coordinated components in a final row. Consumers with more specialized arrangements should implement one custom `IInsightComponent` rather than copy framework components.

## Engine limitations

RimWorld's IMGUI windows are drawn by the game every GUI pass, so a window cannot opt out of the engine's outer repaint loop. Insight Canvas avoids model recomputation in stable passes through model-revision snapshots, layout caches, filtered-list caches, and incremental graph relaxation. Map queries are kept out of repaint; map overlays are transient `MapComponentDraw` registrations.
