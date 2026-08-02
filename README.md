# Insight Canvas

Insight Canvas is a RimWorld 1.6 framework for semantic, interactive visualizations. Dependent mods publish entities, relations, metrics, explanations, events, and actions; the framework coordinates cards, a relationship constellation, an explanation waterfall, an event river, disclosure, and temporary map links.

The installed mod is useful on its own. Open **Mod settings > Insight Canvas > Open Insight Canvas Laboratory**, or use the development-mode **Insight Canvas > Open Laboratory** action. The laboratory demonstrates shared selection, disclosure previews, deterministic graph layout, uncertainty treatment, metric history, and diagnostics.

The assembly has no Harmony dependency. It uses ordinary RimWorld windows, `WindowStack`, debug actions, camera selection, and map-component drawing hooks. Code-drawn visuals are the default so the framework remains usable without external art; optional theme texture paths are supported by the theme model for future content packs.

See [`Documentation/Integration.md`](Documentation/Integration.md) for the public API and architectural constraints.
