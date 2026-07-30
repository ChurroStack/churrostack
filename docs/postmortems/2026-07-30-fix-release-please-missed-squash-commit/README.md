# Fix release-please skipping a merged fix — squash title fell back to the commit subject

**Date:** 2026-07-30
**Area:** GitHub repository merge settings, `.github/workflows/ci.yml` (`pr-title` job)

## Trigger

PR #52 (`fix: group membership and authorization cache invalidation`) merged to
`main` as `ee85766`. The `Release` workflow ran and succeeded, but the open
release PR #51 (`chore: release main`, v1.9.0) was not updated — its changelog
still listed only the `feat` from PR #50:

```
### Features

* **images:** add document-converter image (LibreOffice to PDF API) (#50) (9bb58ec)
```

No **Bug Fixes** section, no entry for #52.

## Root cause

The squash commit that landed on `main` was not a Conventional Commit, so
release-please parsed it, found no recognized type, and ignored it.

```
$ git log -1 --format=%s ee85766
Fix group membership and authorization cache invalidation (#52)
```

The PR *title* was correct (`fix: group membership and authorization cache
invalidation`) and CI's `pr-title` job passed. The mismatch comes from the
repository's squash-merge setting:

```
squash_merge_commit_title: COMMIT_OR_PR_TITLE
```

`COMMIT_OR_PR_TITLE` is GitHub's default, and it does **not** mean "PR title".
It means: use the PR title only when the PR contains **more than one commit**;
when the PR has exactly one commit, use that commit's own subject. PR #52 had a
single commit whose local subject was `Fix group membership and authorization
cache invalidation` — no `fix:` prefix — so that subject became the commit
message on `main`.

This makes the failure mode silent and intermittent-looking: the CI check
validates the PR title, but for single-commit PRs the PR title is not what gets
committed. Both branches of that behaviour are visible in this repo's history:

| PR | commits | first local subject | landed on `main` as |
| --- | --- | --- | --- |
| #50 | 4 | `fix(api): correct postgres deps volume mount path` | `feat(images): add document-converter image … (#50)` — **PR title** |
| #46 | 1 | `feat(templates): add MySQL + phpMyAdmin application template` | same — **commit subject**, happened to be conventional |
| #52 | 1 | `Fix group membership and authorization cache invalidation` | same — **commit subject**, not conventional → dropped |

Every prior release worked only because those PRs happened to have either
multiple commits (#50, #48) or a conventional local commit subject (#46, #44).

Only the changelog was affected. Image publishing is driven by a file diff, not
by commit messages — `release.yml` resolves images via
`tools/changed-images.sh "$prev" "$tag"`, and `ee85766` sits inside the
`v1.8.1..v1.9.0` range, so `churros-api` is still in the publish matrix:

```
$ bash tools/changed-images.sh v1.8.1 ee85766
churros-api, python-streamlit, python-streamlit-x11, document-converter
```

## Fix

1. **Repository setting (root cause).** Switched the squash-merge title source
   so the linted PR title is always what lands on `main`:

   ```
   gh api -X PATCH repos/:owner/:repo -f squash_merge_commit_title=PR_TITLE
   ```

   `squash_merge_commit_message` stays `COMMIT_MESSAGES` (unchanged), so PR
   bodies/commit messages still populate the body. With `PR_TITLE`, the
   `pr-title` CI job is now genuinely authoritative for what release-please
   parses, including single-commit PRs.

2. **Changelog recovery.** `ee85766` cannot be re-parsed without rewriting
   `main`, which the `main` ruleset forbids (`non_fast_forward`,
   `required_linear_history`). Instead this postmortem was merged via a PR
   titled `fix: group membership and authorization cache invalidation`, which
   produces the missing changelog entry. The entry therefore links this
   postmortem commit rather than `ee85766` — the code change itself is already
   on `main` and already in the release range.

3. **Comment correction.** The comment in `ci.yml` above the `pr-title` job
   asserted that the PR title becomes the parsed commit message. That was only
   true for multi-commit PRs; it records the `PR_TITLE` dependency now.

## Verification

- `gh api repos/:owner/:repo --jq .squash_merge_commit_title` returns
  `PR_TITLE`.
- Setting flipped **before** this recovery PR was opened, so this PR is itself
  the first case exercised by the new behavior. Its local commit subject was
  also written conventionally, so it lands correctly under either setting.
- After merge, the `Release` workflow updates PR #51 to still propose
  **1.9.0** — the `feat` from #50 already set the minor bump, and a `fix` does
  not raise it — but now with a **Bug Fixes** section containing
  `group membership and authorization cache invalidation`. A version other
  than 1.9.0 would mean the commit range is not what we think it is.
- Related: [`2026-07-30-fix-group-membership-and-acl-cache`](../2026-07-30-fix-group-membership-and-acl-cache/README.md)
  is the postmortem for the underlying code fix.
