# git-sync "Authentication failed" with a valid PAT

**Date:** 2026-11-16
**Area:** `apps/images/images/git-sync` (`entrypoint.sh`)

## Trigger

A git-sync extension pointed at an Azure DevOps repo kept logging
`fatal: Authentication failed for 'https://…/_git/…/'` every sync period, even
though the configured username + PAT cloned fine on a workstation. The log
showed `Repo URL / Branch / Sync dir / Period` and then the fatal, with **no**
`Cloning repository...` line.

## Root cause

`entrypoint.sh` built `https://<user>:<PAT>@host/...` and used it **only for the
initial `git clone`**. That URL was persisted by git as `remote.origin.url` in
`${SYNC_DIR}/.git/config`. On every later start the clone step was skipped
(`.git` already existed on the persistent code folder) and the sync loop ran
`git fetch origin`, which uses the **stored** URL — never the current
`GIT_REPOSITORY_USERNAME` / `GIT_REPOSITORY_PASSWORD`.

So after a PAT expired/was rotated (or the username changed), updating the
extension settings had no effect: git kept sending the old PAT from
`.git/config` and received a 401.

Side effect: the PAT sat in plain text inside the user's code folder.

## Fix

`apps/images/images/git-sync/entrypoint.sh`:

- Credentials are provided through a git credential helper injected with
  `GIT_CONFIG_COUNT/KEY_0/VALUE_0` that reads the env vars each time git asks.
  They are no longer embedded in the URL, nothing is written to disk, and no URL
  encoding is needed.
- `git remote set-url origin "${GIT_REPOSITORY_URL}"` runs on every start, which
  also scrubs credentials left in existing clones by older versions of the
  script and applies URL changes.
- New trace lines (no secrets): `Auth: credential helper (user=…)` / `Auth: none`
  and `Using existing clone in …`.

`apps/images/AGENTS.md` env-var docs updated accordingly.

## Verification

- `sh -n entrypoint.sh`.
- Local, no network: seeded a clone whose origin held a stale `user:OLD@` URL,
  ran the script's setup, and confirmed `remote get-url origin` returns the plain
  URL and that the helper returns the *current* env values via
  `git credential fill`.
- Real repo (needs a PAT): run the image against a volume that already contains a
  clone made with a bad PAT; expect `Using existing clone`, then successful
  fetches with no 401.

## Immediate workaround for already-affected pods

Delete `/app/home/code/.git` (or the folder) so the next start re-clones, or
update the image — the next start scrubs the stored URL automatically.
