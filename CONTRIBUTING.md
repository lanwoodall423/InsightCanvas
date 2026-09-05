# Contributing to Insight Canvas

Insight Canvas is a public framework mod for RimWorld 1.6. Contributions must preserve documented public APIs, serialization behavior, integration hooks, and downstream-mod compatibility unless a breaking change is explicitly proposed.

## Before opening a pull request

1. Keep changes focused; do not redesign unrelated framework behavior.
2. Run the portable Release harness:

   ```sh
   dotnet run --project Tests/InsightCanvas.CoreTests.csproj --configuration Release
   ```

3. Run the portable release checks:

   ```powershell
   pwsh -NoProfile -File Tools/release_checks.ps1 -Command all
   ```

4. Run RimTest affected validation when the change touches runtime behavior, public integration, Feature Showcase behavior, deployment, or the RimWorld adapter:

   ```powershell
   & (Join-Path $env:RIMTEST_ROOT 'rimtest.cmd') affected --run --json
   ```

5. Do not commit RimWorld or Unity DLLs. Consumers install Insight Canvas separately and must not bundle a duplicate `InsightCanvas.dll`.

## Public API changes

The checked-in `PublicAPI.Shipped.txt` is the compatibility baseline. Compatible additions belong in `PublicAPI.Unshipped.txt` until the next release. Removing or changing a public/protected type, member, enum value, signature, or contract requires a deliberate major-version migration with release notes. Run the API check before requesting review.

## Pull requests

Explain the observable behavior, affected consumers, validation performed, packaging impact, and documentation impact. Reviewers may request downstream integration coverage through RimTest; framework-local tests alone are not sufficient when consumer behavior is affected.

The repository's branch rules require pull requests and passing portable checks before merging. The owner checklist in [`Documentation/RepositoryGovernance.md`](Documentation/RepositoryGovernance.md) records settings that require repository-owner permissions.

## Code and documentation conventions

- Follow the existing namespaces, naming, layout, lifecycle, and renderer-neutral boundaries.
- Keep host ownership explicit; call `PostClose()` from consumer-owned close paths.
- Keep GUI/Text state restoration local to each draw operation.
- Prefer deterministic, bounded work for layout, diagnostics, virtualization, and snapshots.
- Update the changelog and relevant adoption documentation for user-visible changes.
