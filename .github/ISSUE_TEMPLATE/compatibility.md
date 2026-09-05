---
name: Compatibility or integration problem
about: Report a downstream mod, RimWorld, or integration compatibility issue
labels: compatibility
---

## Affected integration

- Consuming mod:
- Insight Canvas version:
- RimWorld version:
- Integration path: embedded host / window / semantic view / renderer / other

## Problem

<!-- Describe what the consumer does and what fails. -->

## Reproduction

<!-- Include a minimal public-API example or exact steps. Do not attach proprietary DLLs. -->

## Lifecycle and packaging impact

- Does this involve `PostClose()` or owner cleanup?
- Does this involve GUI/Text state restoration?
- Does the consumer package contain a duplicate `InsightCanvas.dll`?
- Does this affect load order, serialization, or a public hook?

## Validation evidence

<!-- Include logs, RimTest recipe output, or a minimal project when available. -->
