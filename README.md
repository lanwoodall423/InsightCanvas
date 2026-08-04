# Insight Canvas

Insight Canvas is a RimWorld 1.6 framework for semantic, interactive visualizations. Dependent mods publish entities, relations, metrics, explanations, events, and actions; the framework coordinates cards, a relationship constellation, an explanation waterfall, an event river, disclosure, and temporary map links.

The installed mod is useful on its own. Open **Mod settings > Insight Canvas > Open Insight Canvas Laboratory**, or use the development-mode **Insight Canvas > Open Laboratory** action. The laboratory demonstrates shared selection, disclosure previews, deterministic graph layout, uncertainty treatment, metric history, and diagnostics.

The assembly has no Harmony dependency. It uses ordinary RimWorld windows, `WindowStack`, debug actions, camera selection, and map-component drawing hooks. Code-drawn visuals are the default so the framework remains usable without external art; optional theme texture paths are supported by the theme model for future content packs.

See [`Documentation/Integration.md`](Documentation/Integration.md) for the public API and architectural constraints.

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
- Set `RimWorldDir` and build the Release mod assembly locally.
- Confirm `1.6/Assemblies/InsightCanvas.dll` and `.xml` are present and no proprietary DLLs were added to the package.
- Install the package in RimWorld 1.6 and exercise the laboratory, map overlays, timeline, graph, and serialization integrations.
- Run `git diff --check` and review the package metadata before distribution.

## Serialization boundary

`InsightModelSerialization` writes deterministic XML schema version 2. Entities, relations, metrics and history, events, badges, manual graph positions, explanations, action metadata, and explicit source/icon identifiers are pure model data and round-trip. Runtime delegates, live source objects, textures, map references, and callbacks are not serialized. Loaded actions are deliberately disabled and non-invokable; use the diagnostics report to rebind runtime behavior in the consuming mod.

The reader also accepts the prior unversioned XML format. `InsightModel.Validate()` accumulates errors and warnings with collection, ID, and reason context instead of stopping at the first invalid reference.

## License

No license has yet been granted for Insight Canvas. Reuse, modification, or distribution requires the author’s permission. Selecting and adding an SPDX license is an owner decision and has intentionally not been made here.
