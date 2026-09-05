## Summary

<!-- What observable behavior changes? -->

## Compatibility checklist

- [ ] Does this modify public API? If yes, describe the change and update the API baseline/unshipped list.
- [ ] Is it backward compatible? If no, include the intentional major-version migration and release notes.
- [ ] Were portable Release tests run?
- [ ] Was RimTest run when applicable?
- [ ] Does release packaging change?
- [ ] Does the changelog need updating?

## Validation

- [ ] `dotnet run --project Tests/InsightCanvas.CoreTests.csproj --configuration Release`
- [ ] `pwsh -NoProfile -File Tools/release_checks.ps1 -Command all`
- [ ] `rimtest affected --run --json` when runtime/integration behavior is affected

## Review notes

<!-- Call out lifecycle ownership, downstream compatibility, documentation, and known limitations. -->
