# Changelog

## 2.1.0 - 2026-08-12

This compatible minor release bridges retained semantic views into composable v2 documents.

- Added public `InsightUi.SemanticView(id, model, view, context)` and retained source replacement APIs for existing semantic integrations.
- Added revision-keyed immutable snapshot caching, semantic layout invalidation/deferred-refresh diagnostics, bounded contained render errors, and inheritance of document accessibility, density, reduced motion, bounds, delta time, and host overlay ownership.
- Preserved `InsightCanvasHost`, `InsightWindow`, and all v1 semantic APIs; new integrations can compose ordinary and semantic elements in one `InsightUiDocument`.
- Added portable lifecycle coverage and a public-API Frontier consumer fixture.

The current package targets RimWorld 1.6 and uses package ID `lan.insightcanvas`. The assembly version is `2.1.0.0`. Owner license selection required; no license is granted by this repository until the owner selects one.

## 2.0.0 - 2026-08-12

This is the breaking v2 redesign. Insight Canvas is now a general-purpose, opt-in UI toolkit and design system; ordinary consumers no longer need to construct an `InsightModel`.

- Added `InsightUiDocument`, `InsightUiHost`, and `InsightUiWindow` for document-local state, embedding, and complete RimWorld windows.
- Added composable rows, columns, wrapping, grids, split panes, scroll regions, responsive navigation, controls, bindings, focus, themes, accessibility settings, custom drawing capabilities, and bounded virtualization.
- Added document-owned effects, highlights, reveal/fade transitions, toasts, popovers, and transient cleanup.
- Replaced the primary demonstration with the Feature Showcase, covering the public API across responsive pages and diagnostics.
- Retained graph, timeline, explanation, map bridge, serialization, constellation, and event components as optional advanced extensions.
- Improved stack composition ergonomics: stack factories now preserve `InsightUiStack` through fluent style setters, so consumers can call `Add` without casts or compatibility extensions.
- Added adoption documentation, focused public examples, dependency metadata guidance, and a release checklist.

The current package targets RimWorld 1.6 and uses package ID `lan.insightcanvas`. The assembly version is `2.0.0.0`. Owner license selection required; no license is granted by this repository until the owner selects one.

## 1.0.1

- Added `InsightCanvasHost` for embedding retained views in host windows such as main tabs.
- Added `InsightModel.Clear()` for safe outside-repaint replacement of a live publication.
- Extended coordinated view layout with a retained bottom row for more than four components.

## 1.0.0

- Added the RimWorld 1.6 Insight Canvas framework assembly.
- Added semantic models for entities, relations, metrics, actions, explanations, events, disclosure, and time ranges.
- Added retained responsive layout, scoped IMGUI state restoration, theme tokens, accessibility palette support, and diagnostic counters.
- Added Insight Cards, Living Constellation, Explanation View, Event River, and temporary Map Bridge actions.
- Added the standalone deterministic semantic demo and deterministic core test harness.
