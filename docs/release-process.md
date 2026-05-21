# Release Process

ChurroStack uses an aggregated, [release-please](https://github.com/googleapis/release-please)-driven
release flow on GitHub Actions. This document explains how code gets from a
pull request to a versioned container image on `quay.io/churrostack`.

## TL;DR

1. Open a PR with a **Conventional Commit** title. CI lints, tests and
   build-validates only the affected projects/images.
2. Merge the PR (**squash merge**). release-please accumulates merged changes
   into a single open **release PR**.
3. Merge the **release PR** when you want to cut a release. That tags one
   unified `vX.Y.Z` and CI builds & pushes `<image>:X.Y.Z` + `<image>:latest`
   for every image whose files changed since the last release.

## Versioning model

- **One unified version for the whole repo.** A release produces a single git
  tag `vX.Y.Z` and a single `CHANGELOG.md`. The version lives in the root
  `package.json` and is mirrored into `apps/ui/package.json`.
- **Per-image artifacts.** Each container image is published as
  `quay.io/churrostack/<name>:X.Y.Z` (and `:latest`). Only images whose files
  changed since the previous release are rebuilt — unchanged images keep their
  existing tags. So after `v1.5.0` you may have `churros-api:1.5.0` while
  `churros-ui` is still only at `1.4.0`. That is expected and intentional
  (efficient builds).
- The version is injected into .NET assemblies at build time via the
  `VERSION` Docker build-arg (`/p:Version`).

## Conventional Commits

The PR **title** must follow [Conventional Commits](https://www.conventionalcommits.org/)
because PRs are squash-merged — the title becomes the commit message that
release-please parses. CI enforces this (`pr-title` check).

| Prefix | Effect on the version | Use for |
|--------|-----------------------|---------|
| `fix:` | patch bump (`x.y.Z`) | bug fixes |
| `feat:` | minor bump (`x.Y.z`) | new features |
| `feat!:` / `fix!:` or a `BREAKING CHANGE:` footer | major bump (`X.y.z`) | breaking changes |
| `chore:`, `docs:`, `refactor:`, `test:`, `ci:`, `build:` | no bump | everything else |

release-please decides which images changed by file path — no commit scope is
required. A `feat:` PR touching `apps/api/**` bumps the version and, on
release, rebuilds `churros-api`.

## What runs when

### On every pull request — `.github/workflows/ci.yml`

- **pr-title** — validates the Conventional Commit PR title.
- **validate** — `nx affected -t lint test build` (Node + .NET toolchains).
  Test/lint targets that don't exist yet are skipped; they are picked up
  automatically once added.
- **docker-validate** — builds the Dockerfile of every changed image
  (`push: false`) to catch breakage early.

### On merge to `main` — `.github/workflows/release.yml`

- **release-please** — creates or updates the aggregated release PR. No images
  are built on a normal merge.
- When the **release PR** is merged, release-please cuts the `vX.Y.Z` tag +
  GitHub Release, then the **publish** job builds and pushes
  `<image>:X.Y.Z` + `<image>:latest` for every image changed since the
  previous release tag. (The first release builds all images once.)

## The image manifest

[`.github/images.json`](../.github/images.json) is the single source of truth
for every container image: its `name`, build `context`, `dockerfile`, the
`watch` paths that trigger a rebuild, and whether it takes a `VERSION`
build-arg. Both CI and the local `build.sh` scripts read it — there is no
build logic duplicated anywhere else.

To add a new image: add an entry to `.github/images.json` and a `build.sh`
wrapper next to its Dockerfile.

## Building images locally

Each app keeps a `build.sh` for local testing. It is a thin wrapper over
[`tools/build-image.sh`](../tools/build-image.sh):

```sh
# Build a local image for testing — host arch, NO push:
./build.sh
#   -> quay.io/churrostack/<name>:0.0.1-local  (and :local)

# Manually build & push a specific version (rarely needed — CI does this):
./build.sh 1.2.3 --push
#   -> linux/amd64, pushed to quay.io as :1.2.3 and :latest
```

Local builds never touch the `:latest` tag, so a dev build cannot shadow a
released image in your local Docker daemon.

## One-time repository setup

These must be configured once by a maintainer:

- **Quay credentials** — create a Quay **robot account** with write access to
  the `churrostack` org. Add a GitHub **Environment** named `production` with
  secrets `QUAY_USERNAME` and `QUAY_ROBOT_TOKEN`.
- **Actions permissions** — Settings → Actions → General → enable
  *"Allow GitHub Actions to create and approve pull requests"* (release-please
  opens the release PR).
- **Merge strategy** — Settings → General → allow **squash merging only**.
- **Branch protection** on `main` — require the `validate` and
  `docker-validate` status checks and require pull requests.

> Note: the release PR is opened by `GITHUB_TOKEN`, so it does not itself
> trigger `ci.yml`. That is acceptable — it only edits `CHANGELOG.md` and
> version files. To run CI on the release PR too, give release-please a GitHub
> App token / PAT instead of `GITHUB_TOKEN`.
