# Insight Canvas release checklist

Use this checklist for each versioned release. A release is not complete until the framework, documentation, package metadata, and generated assembly agree.

## Build and automated validation

- [ ] Portable tests pass in Release configuration.
- [ ] Public API compatibility passes against `PublicAPI.Shipped.txt`; compatible additions are reviewed in `PublicAPI.Unshipped.txt`.
- [ ] Release version consistency check passes.
- [ ] Canonical package script succeeds, package inspection passes, and the SHA-256 manifest exists.
- [ ] Checked-in `1.6/Assemblies/InsightCanvas.dll` and XML pass the deterministic freshness comparison.
- [ ] RimWorld 1.6 `net472` Release assembly builds with zero warnings and zero errors.
- [ ] DevBridge2 registers `insightcanvas-in-game-suite` with the canonical `insight-canvas` project alias and authenticated companion operation.
- [ ] `rimtest affected --run --json` passes with current-source artifact freshness established.
- [ ] The Feature Showcase companion report passes its ten-page rendering, interaction, semantic-view, diagnostics, and owner-cleanup assertions.
- [ ] Feature Showcase opens through its normal settings/debug entry points.
- [ ] Feature Showcase visits every page at narrow and wide widths without captured render errors or invalid geometry.
- [ ] Embedded host lifecycle is covered, including `PostClose()` cleanup.
- [ ] Window focus, Tab/Shift+Tab traversal, and keyboard activation remain usable.
- [ ] Normal, compact, comfortable, high-contrast, and reduced-motion settings render correctly.
- [ ] Effects, toasts, popovers, dropdowns, and other transient UI close when their owner closes.
- [ ] Virtualized collections retain bounded visible/cache ranges and deterministic item order.
- [ ] Custom drawing and icon fallbacks work when optional renderer capabilities are unavailable.
- [ ] GUI/Text state is restored after every framework draw and custom painter callback.
- [ ] `InsightUi.SemanticView` composes retained model/view/context instances with ordinary elements through normal Measure/Arrange/Paint.
- [ ] Semantic snapshots are immutable and revision-keyed, with no per-frame rebuild and deferred refresh when a model changes during navigation.
- [ ] Semantic lifecycle tests cover independent contexts, root replacement, model/view/context replacement, accessibility/density/reduced motion, resize, duplicate IDs, contained errors, bounded diagnostics, overlay cleanup, and host close/reopen.
- [ ] Semantic paint inherits the enclosing host owner without nesting owners and does not query maps/worlds during ordinary repaint.

## Adoption and package checks

- [ ] README and Quickstart show Window, embedded, binding, theme, effects, and custom-rendering paths.
- [ ] Integration documentation identifies Core UI, Effects and polish, and optional Advanced extensions.
- [ ] Package ID remains `lan.insightcanvas`; supported game version remains RimWorld 1.6.
- [ ] Consumer dependency/load-order guidance names `lan.insightcanvas`.
- [ ] The release package contains one framework DLL; consuming mods do not bundle a duplicate `InsightCanvas.dll`.
- [ ] `Examples/MinimalConsumer/InsightCanvas.MinimalConsumer.csproj` compiles against the checked-in released-style `InsightCanvas.dll` without copying it into the sample output.
- [ ] A clean extraction of the canonical ZIP produces the expected mod tree; a clean in-game install is manually verified.
- [ ] RC and stable publication metadata, notes, package names, and checksums match `Documentation/ReleasePublication.md`.
- [ ] Proprietary RimWorld/Unity DLLs are excluded from distribution.
- [ ] Assembly version `2.1.1`, `About.xml` `<modVersion>`, README version, changelog entry, and release notes are synchronized.
- [ ] Changelog includes migration notes for any breaking public API change.
- [ ] GPLv3 is identified consistently in the repository and the canonical GPLv3 text is present in `LICENSE`.

## Final hygiene

- [ ] No user-facing obsolete legacy-demo terminology remains.
- [ ] No global GUI skin mutation or Harmony dependency was added.
- [ ] Unrelated worktree changes are preserved.
- [ ] `git diff --check` passes.
- [ ] No commit or push is made without explicit owner request.
