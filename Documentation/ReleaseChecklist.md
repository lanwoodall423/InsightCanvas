# Insight Canvas release checklist

Use this checklist for each versioned release. A release is not complete until the framework, documentation, package metadata, and generated assembly agree.

## Build and automated validation

- [ ] Portable tests pass in Release configuration.
- [ ] RimWorld 1.6 `net472` Release assembly builds with zero warnings and zero errors.
- [ ] Feature Showcase opens through its normal settings/debug entry points.
- [ ] Feature Showcase visits every page at narrow and wide widths without captured render errors or invalid geometry.
- [ ] Embedded host lifecycle is covered, including `PostClose()` cleanup.
- [ ] Window focus, Tab/Shift+Tab traversal, and keyboard activation remain usable.
- [ ] Normal, compact, comfortable, high-contrast, and reduced-motion settings render correctly.
- [ ] Effects, toasts, popovers, dropdowns, and other transient UI close when their owner closes.
- [ ] Virtualized collections retain bounded visible/cache ranges and deterministic item order.
- [ ] Custom drawing and icon fallbacks work when optional renderer capabilities are unavailable.
- [ ] GUI/Text state is restored after every framework draw and custom painter callback.

## Adoption and package checks

- [ ] README and Quickstart show Window, embedded, binding, theme, effects, and custom-rendering paths.
- [ ] Integration documentation identifies Core UI, Effects and polish, and optional Advanced extensions.
- [ ] Package ID remains `lan.insightcanvas`; supported game version remains RimWorld 1.6.
- [ ] Consumer dependency/load-order guidance names `lan.insightcanvas`.
- [ ] The release package contains one framework DLL; consuming mods do not bundle a duplicate `InsightCanvas.dll`.
- [ ] Proprietary RimWorld/Unity DLLs are excluded from distribution.
- [ ] Assembly version, README version, changelog entry, and release notes are synchronized.
- [ ] Changelog includes migration notes for any breaking public API change.
- [ ] License status is resolved before distribution, or the release report says exactly: **Owner license selection required.**

## Final hygiene

- [ ] No user-facing obsolete legacy-demo terminology remains.
- [ ] No global GUI skin mutation or Harmony dependency was added.
- [ ] Unrelated worktree changes are preserved.
- [ ] `git diff --check` passes.
- [ ] No commit or push is made without explicit owner request.
