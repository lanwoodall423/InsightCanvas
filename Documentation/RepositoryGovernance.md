# Repository governance and metadata

This page records the owner-controlled GitHub settings for `lanwoodall423/InsightCanvas`. Branch protection and repository metadata were applied through authenticated GitHub owner tooling where available; the license classifier still requires the prepared local changes to reach the remote default branch.

## Required `main` branch rules

In **Settings > Branches > Branch protection rules** (or the repository ruleset UI), protect `main` with:

- Require a pull request before merging.
- Require at least one approving review.
- Dismiss stale approvals when new commits are pushed, if the owner wants review freshness enforced.
- Require status checks to pass before merging; select the portable CI check/job for `.github/workflows/ci.yml`, including the portable Release harness, API compatibility, version, and package checks.
- Require branches to be up to date before merging when GitHub exposes that option.
- Require conversation resolution before merging.
- Disable force pushes.
- Disable branch deletion.
- Allow the owner/admin bypass only when intentional and documented.

In **Settings > General > Pull Requests**, enable automatic deletion of head branches after pull requests merge. This does not delete `main`.

## Repository metadata target

- Description: `Composable, opt-in UI framework and design system for RimWorld 1.6 mod authors.`
- Topics: `rimworld`, `rimworld-mod`, `csharp`, `ui-framework`, `modding`, `game-ui`, `design-system`
- Default branch: `main`
- License: GNU General Public License v3.0, matching the checked-in canonical `LICENSE` and `NOTICE`.

The authenticated GitHub API now reports the requested description and topics, `main` as the default branch, one required approving review, strict current-branch checks, resolved-conversation enforcement, disabled force pushes, disabled branch deletion, and automatic head-branch deletion. It still reports license `Other`/`NOASSERTION` because the prepared canonical GPLv3 `LICENSE` is not yet on the remote default branch. Recheck after pushing; if GitHub still reports `Other`, select GPL-3.0 in the repository metadata or add the repository's license metadata through the owner-controlled GitHub UI/API.

## Owner verification

After pushing the prepared changes, confirm:

1. `main` protection requires the pull request and portable CI checks.
2. Force pushes and branch deletion are disabled.
3. Conversation resolution and current-branch requirements are enabled where supported.
4. Merged feature branches are automatically deleted.
5. The description and topics match the values above.
6. GitHub recognizes GPL-3.0 rather than `Other`.
7. No stable tag or GitHub Release is created until final approval.
