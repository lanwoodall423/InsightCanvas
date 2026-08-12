# Insight Canvas

Insight Canvas 2.0.0 is an opt-in composable UI toolkit and design system for RimWorld 1.6 mod authors. It gives ordinary mod screens a cohesive visual language, responsive layout, scoped state, accessible controls, and lightweight polish without globally reskinning RimWorld or requiring `InsightModel`.

Use it when a screen needs more than a pile of `Widgets.*` calls but should still feel native to RimWorld. Build an element tree once, provide stable IDs, and choose either a normal RimWorld `Window` or a caller-owned `Rect`. The document owns state and effects for that screen, so two windows do not leak selection, expansion, focus, scroll, theme, or transient feedback into one another.

The installed mod includes **Feature Showcase**, a dogfooded demonstration application. Open it from **Mod settings > Insight Canvas > Open Feature Showcase** or from the development-mode **Insight Canvas > Open Feature Showcase** action. It covers overview, foundations, layout, controls, workspaces, data display, motion, accessibility, advanced widgets, and diagnostics.

## Why mod authors use it

- Compose rows, columns, wrapping, adaptive grids, split panes, scrolling, responsive navigation, and virtualized lists without hand-written layout arithmetic.
- Use stable-ID controls with either document-local state or getter/setter bindings to an existing mod settings/model object.
- Adopt the RimWorld+ default theme, density modes, high contrast, reduced motion, focus traversal, tooltips, badges, cards, and restrained transitions.
- Embed into an existing window or open the same document in an `InsightUiWindow`.
- Draw icons, custom previews, and small charts through renderer-neutral capability interfaces while keeping the normal measure/arrange/paint phases.
- Add graph, timeline, explanation, event, constellation, serialization, and map-link features only when a consumer actually needs them.

## Minimal Window

This is the smallest complete public-API path. It does not create a semantic model or a manual state dictionary.

```csharp
using InsightCanvas;
using Verse;

public static class MinimalWindowExample
{
    public static void Open()
    {
        InsightUiStack root = InsightUi.Column("settings-root",
            InsightUi.Label("title", "Colony settings", InsightUiTextStyle.Title),
            InsightUi.Surface("settings-card", InsightUi.Column("settings-body",
                InsightUi.Toggle("show-hints", "Show hints"),
                InsightUi.Button("apply", "Apply")))
        ).SetGap(10f).SetPadding(12f);

        InsightUiDocument document = new InsightUiDocument("Colony settings", root);
        Find.WindowStack.Add(new InsightUiWindow(document));
    }
}
```

## Embedded panel

When another mod owns the window, keep an `InsightUiHost` with the document and give it the rectangle from `DoWindowContents`. Call `PostClose()` from the owner’s close path so document effects, transient menus, focus, and map overlays are released.

```csharp
public sealed class EmbeddedPanelExample
{
    private readonly InsightUiHost host;

    public EmbeddedPanelExample()
    {
        InsightUiStack root = InsightUi.Column("panel-root",
            InsightUi.Label("title", "Research brief", InsightUiTextStyle.Heading),
            InsightUi.Progress("confidence", 0.72f, InsightTheme.Default.Selected),
            InsightUi.Button("refresh", "Refresh"))
            .SetGap(8f).SetPadding(12f);
        host = new InsightUiHost(new InsightUiDocument("Research panel", root));
    }

    public void Draw(UnityEngine.Rect rect)
    {
        host.Draw(rect, UnityEngine.Time.deltaTime);
    }

    public void Close()
    {
        host.PostClose();
    }
}
```

The same `InsightUiDocument` can be passed to `InsightUiWindow`, used with `InsightUiHost`, or rendered through `InsightUiRenderer.Draw(rect, document)` when a consumer already manages host ownership.

## The public toolkit in three layers

### Core UI

Documents, hosts, windows, rows, columns, wrapping, grids, split panes, scroll regions, surfaces, cards, labels, badges, dividers, progress, buttons, icon buttons, toggles, sliders, text fields, selectors, expanders, tabs, navigation, breadcrumbs, searchable fields, popovers, toolbars, scoped state, bindings, focus, themes, accessibility, icons, custom drawing, and virtualization.

### Effects and polish

`InsightUi.Fade`, `Reveal`, and `Highlight` provide keyed transitions. `document.Effects` provides short-lived flashes and `document.Toasts` provides document-local feedback. Motion is subtle, interruptible, and settles immediately when `ReducedMotion` is enabled; it does not silently change layout.

### Advanced extensions

`InsightModel`, graph layout, timeline clustering, explanations, event/constellation widgets, serialization, and the map bridge remain available as optional adapters. They are not prerequisites for an ordinary settings screen or embedded panel.

## Bindings, themes, and custom visuals

Stateful controls can bind directly to consumer-owned state:

```csharp
InsightUiToggle hints = InsightUi.Toggle("hints", "Show hints")
    .Bind(() => settings.ShowHints, value => settings.ShowHints = value);
InsightUiSlider volume = InsightUi.Slider("volume", 0.8f, 0f, 1f)
    .Bind(() => settings.Volume, value => settings.Volume = value);
InsightUiSelect density = InsightUi.Select("density", "Density",
    new[] { "Comfortable", "Normal", "Compact" })
    .Bind(() => settings.Density, value => settings.Density = value);
```

The getter is authoritative on later frames, so external changes are reflected without a second synchronized copy. Use `InsightUi.Scope("audio", child)` when a reusable component needs an identity prefix.

Themes are document-scoped. Clone a token set, change only what the screen needs, and assign it to that document:

```csharp
InsightTheme theme = InsightTheme.Default.Clone();
theme.Selected = new InsightColor(0.78f, 0.55f, 0.24f);
document.Theme = theme;
document.Density = InsightUiDensity.Compact;
document.HighContrast = true;
document.ReducedMotion = true;
document.Invalidate();
```

`InsightUi.Custom` is the supported escape hatch for code-drawn previews. Use `IInsightUiCustomPainter`, `IInsightUiIconPainter`, and the supplied `InsightUiFrame`; do not mutate `GUI.skin` globally. The RimWorld renderer restores Unity GUI/Text state after each draw.

## Installation and dependency behavior

Insight Canvas is packaged as the RimWorld mod with package ID `lan.insightcanvas`, targeting RimWorld 1.6. A consuming mod should install Insight Canvas as a separate mod and declare it explicitly in its own `About/About.xml`:

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

The dependency declares the required load relationship; the explicit `loadAfter` is useful when a consuming mod also has optional integration code. Reference `InsightCanvas.dll` from the installed mod’s `1.6/Assemblies` directory at compile time, but do not copy or bundle a second `InsightCanvas.dll` in the consumer package. RimWorld’s proprietary assemblies are local build inputs and must not be redistributed.

## Versioning

Insight Canvas follows semantic versioning for its documented public API:

- `MAJOR` changes may remove or break public API and require migration notes.
- `MINOR` adds public API and compatible components.
- `PATCH` fixes behavior, documentation, tests, and packaging without intentional API breaks.

The current v2 release is `2.0.0` (`AssemblyVersion`/`AssemblyFileVersion` `2.0.0.0`). See [`CHANGELOG.md`](CHANGELOG.md) for the v1-to-v2 migration boundary. The assembly, README, changelog, and release checklist are kept in sync for each release; RimWorld’s `About.xml` has no portable version field, so the package ID and supported game version remain the authoritative mod metadata.

## Build and validation

- [`Documentation/Quickstart.md`](Documentation/Quickstart.md) — adoption recipes and lifecycle guidance.
- [`Documentation/Integration.md`](Documentation/Integration.md) — complete public API reference and optional semantic extensions.
- [`Documentation/Testing.md`](Documentation/Testing.md) — portable and mod-owned in-game testing.
- [`Documentation/ReleaseChecklist.md`](Documentation/ReleaseChecklist.md) — release gate.
- [`Examples/`](Examples/) — focused public-API-only copy/paste examples.

Portable checks run with:

```sh
dotnet run --project Tests/InsightCanvas.CoreTests.csproj --configuration Release
```

The RimWorld assembly is a `net472` Release build and requires a local RimWorld 1.6 installation. Set `RimWorldDir` to that installation and run:

```powershell
dotnet build Source/InsightCanvas.csproj --configuration Release --nologo
```

The build writes `1.6/Assemblies/InsightCanvas.dll` and its XML documentation file.

## License

Owner license selection required.

No license is added autonomously. The owner can select MIT, Apache-2.0, or MPL-2.0 and add the corresponding notice before distribution. Until then, reuse, modification, and redistribution require the owner’s permission.
