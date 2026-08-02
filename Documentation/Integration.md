# Insight Canvas Integration

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
