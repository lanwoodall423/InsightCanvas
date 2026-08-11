# Insight Canvas

Insight Canvas is a RimWorld 1.6 opt-in UI framework and design system for mod authors. It provides composable rows, columns, adaptive grids, split panes, scroll regions, stateful controls, scoped themes, virtualization, and a conventional Window shell without globally changing vanilla or third-party UI.

The installed mod is useful on its own. Open **Mod settings > Insight Canvas > Open Feature Showcase**, or use the development-mode **Insight Canvas > Open Feature Showcase** action. The showcase demonstrates responsive layout, stable document state, controls, virtualization, scoped themes, density, and accessibility. The earlier semantic Laboratory remains an optional advanced extension for graph, explanation, event, and map-link consumers.

The assembly has no Harmony dependency and never mutates the global GUI skin. It uses ordinary RimWorld windows, `WindowStack`, debug actions, and a caller-owned `Rect` embedding entry point. Code-drawn visuals are the default so the framework remains usable without external art; optional theme texture paths remain available to advanced extensions.

See [`Documentation/Integration.md`](Documentation/Integration.md) for the public API and architectural constraints.

## Composable UI v2

Ordinary UI does not require an `InsightModel`. Build an element tree, give it a document-owned state store and theme, then either embed it or open it as a Window:

```csharp
InsightUiElement root = InsightUi.Column("settings",
    InsightUi.Label("title", "Colony settings", InsightUiTextStyle.Title),
    InsightUi.Surface("general", InsightUi.Column("general-content",
        InsightUi.Toggle("show-hints", "Show hints"),
        InsightUi.Button("apply", "Apply", ApplySettings))),
    InsightUi.Grid("cards", 220f)
        .Add(InsightUi.Badge("status", "Ready"), InsightUi.Progress("progress", 0.72f)))
    .SetGap(10f)
    .SetPadding(12f);

InsightUiDocument document = new InsightUiDocument("Settings", root);
Find.WindowStack.Add(new InsightUiWindow(document));
```

For an existing host window, keep the `InsightUiDocument` and `InsightUiHost`, then call `host.Draw(rect, deltaTime)` from `DoWindowContents`. The document owns selection, tab, expansion, focus, and scroll state; themes and accessibility options are scoped to that document. `InsightUiRenderer.Draw(rect, document)` is also available as a direct embedding entry point.

## Supported version and prerequisites

- Supported game version: RimWorld 1.6 only. The package metadata, source description, and mod folder all target `1.6`.
- Portable checks require the .NET 8 SDK.
- The real mod build additionally requires a local RimWorld 1.6 installation, its managed DLLs, and a .NET SDK/targeting setup that can build `net472`.
- RimWorld assemblies are proprietary game files. They are not committed to this repository, downloaded by CI, or copied into the package.

## Portable checks

From the repository root, this single command restores dependencies, builds, and runs every non-RimWorld regression check:

```sh
dotnet run --project Tests/InsightCanvas.CoreTests.csproj --configuration Release
```

The process exits nonzero if restore, compilation, or the harness fails. GitHub Actions runs this same portable command on pushes and pull requests. CI therefore verifies the model, serialization, validation, layout, timeline, and other game-independent behavior; it does not claim to compile or execute the RimWorld-facing assembly.

## Local mod build

Set `RimWorldDir` to the RimWorld installation directory. The project derives the managed directory for the platform and accepts paths containing spaces.

Windows PowerShell:

```powershell
$env:RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld'
dotnet build Source/InsightCanvas.csproj --configuration Release --nologo
```

Linux:

```sh
export RimWorldDir="$HOME/.local/share/Steam/steamapps/common/RimWorld"
dotnet build Source/InsightCanvas.csproj --configuration Release --nologo
```

macOS:

```sh
export RimWorldDir="$HOME/Library/Application Support/Steam/steamapps/common/RimWorld"
dotnet build Source/InsightCanvas.csproj --configuration Release --nologo
```

The equivalent explicit property is `/p:RimWorldDir=<path>`. If it is unset or does not contain the expected 1.6 managed DLL layout, the build stops with an actionable error. A successful Release build writes `1.6/Assemblies/InsightCanvas.dll` and `1.6/Assemblies/InsightCanvas.xml`; those paths are part of the mod package alongside `About/` and `1.6/Languages/`.

Quote the property argument when the installation path contains spaces, for example PowerShell `dotnet build Source/InsightCanvas.csproj --configuration Release "/p:RimWorldDir=C:\Program Files (x86)\Steam\steamapps\common\RimWorld"` or POSIX shells `dotnet build Source/InsightCanvas.csproj --configuration Release '/p:RimWorldDir=/opt/My Games/RimWorld'`.

## Pre-release checklist

- Run the portable command above from a clean checkout.
- Require a visible green GitHub Actions run for the exact commit under release review.
- Set `RimWorldDir` and build the Release mod assembly locally.
- Confirm `1.6/Assemblies/InsightCanvas.dll` and `.xml` are present and no proprietary DLLs were added to the package.
- Install the package in RimWorld 1.6 and complete an in-game smoke test covering the Feature Showcase, embedding/window lifecycle, and preserved semantic extensions such as map overlays, timeline, graph, and serialization integrations.
- Run `git diff --check` and review the package metadata before distribution.

## Serialization boundary

`InsightModelSerialization` writes deterministic XML schema version 2. Entities and manual positions are sorted by ordinal entity ID; owner-keyed dictionaries and unordered relations are canonically ordered; event, badge, metric, action, history, and explanation-operation list order is preserved because those sequences can affect display or interpretation. Runtime delegates, live source objects, textures, map references, and callbacks are not serialized. Loaded actions are deliberately disabled and non-invokable; use the diagnostics report to rebind runtime behavior in the consuming mod.

The reader also accepts the prior unversioned XML format. `InsightModel.Validate()` accumulates errors and warnings with collection, ID, and reason context instead of stopping at the first invalid reference.

Configured action intent is separate from runtime executability. `ConfiguredEnabled` is the integration’s saved enabled/disabled choice. `Enabled` is true only when that choice is true and a callback is currently bound. Deserialization preserves `ConfiguredEnabled` but leaves `Callback` null and `Enabled` false. Rebind safely through the public API rather than reflection:

```csharp
InsightModelSerializationReport loaded = InsightModelSerialization.DeserializeWithDiagnostics(xml);
InsightModel restored = loaded.Model;
restored.RebindAction("species:trout", "inspect", () => Log.Message("Inspect trout"));
```

`RebindAction` replaces the matching action while preserving its configured intent. `InsightAction.Rebind(callback)` is also available when rebuilding an action for a new model; do not add it beside the existing same-ID action. Invocation remains a safe no-op until a callback is present and the configured intent permits it.

## License

No license has yet been granted for Insight Canvas. Reuse, modification, or distribution requires the author’s permission. Selecting and adding an SPDX license is an owner decision and has intentionally not been made here.
