# images — Agent Guide

The repository-wide agent guide at [`AGENTS.md`](../../AGENTS.md) (monorepo
root) takes precedence over this file — read it first.

## Overview

`apps/images` is a collection of tailored Docker container images. These are
not application services of the monorepo — they are the **base/runtime images
that ChurroStack deploys as user workloads**: Python notebooks/apps,
code-server IDEs, vLLM model servers, web terminals, file browsers and a
git-sync sidecar.

Each image lives in its own subdirectory under `images/` and is fully
self-contained (Dockerfile + helper scripts + bundled binaries). There is no
shared build chain between images; the `_old/` `base` → `streamlit` layering
has been retired (see [Deprecated images](#deprecated-images)).

## The Nx `docker-build` target

`project.json` defines a single target. It runs from `cwd: apps/images` and
interpolates the `image` arg into both the tag and the build context path:

```
docker build -t churrostack/{args.image}:latest ./{args.image}
```

Invoke it from the repo root, passing the image name via `--args`:

```
pnpm nx docker-build images --args="--image=python"
```

Gotchas:

- The target's build context is `./{args.image}` relative to `apps/images`,
  but the image subdirectories actually live under `apps/images/images/`. To
  use the Nx target as written you must pass the nested path, e.g.
  `--args="--image=images/python"` — which then tags the image
  `churrostack/images/python:latest`. The target predates the `images/`
  subdirectory layout; prefer the per-image `build.sh` (or CI) for correct tags.
- The Nx target does not push and does not set `--platform`.

## Images

All images run as a non-root user (`appuser`, UID/GID `999`, configurable via
the `USER_UID` / `USER_GID` build args) with home at `/app` and a working dir
of `/app/home`. Bundled `ttyd.x86_64` and `google-chrome` wrapper files are
checked into the repo (no download at build time).

| Image | Base | Purpose |
| --- | --- | --- |
| `python` | `python:3.13-slim` | Python app/Streamlit runtime: venv with streamlit/fastapi/pandas/etc., MS SQL ODBC drivers (`msodbcsql18`, `mssql-tools18`), bundled `ttyd` web terminal. `init.sh` (de)installs `requirements.txt` and restart-loops the user's Python entrypoint. Ports 3001/8000/8501. |
| `python-x11` | `python:3.13-slim` | Same as `python` plus a virtual X11 desktop: Xvfb + OpenBox + x11vnc + noVNC/websockify, Firefox-ESR and Google Chrome (via the `google-chrome` headless wrapper). For GUI/browser automation workloads. Adds port 3003 (noVNC). |
| `python-torch-nvidia` | `pytorch/pytorch:2.10.0-cuda12.6-cudnn9-runtime` | GPU/CUDA Python runtime with PyTorch and `ffmpeg`, plus bundled `ttyd` and the same `init.sh` restart-loop. Ports 3001/8000/8501. |
| `code-server` | `python:3.13-slim` | Browser-based VS Code (`code-server`) with the Continue and ms-python extensions, git/git-lfs, zsh. `init.sh` runs it with `--auth none` on `0.0.0.0:8080`. |
| `vllm-openai` | `vllm/vllm-openai:latest` | OpenAI-compatible LLM inference server. Thin overlay that adds the non-root `appuser` and fixes flashinfer dir permissions. Port 8001; inherits the upstream entrypoint. |
| `ttyd` | `alpine` | Minimal standalone web terminal (`ttyd` + bash, `tini` as PID 1). Port 3001. |
| `file-browser` | `debian:bookworm-slim` | Standalone [filebrowser](https://filebrowser.org) web file manager. `init.sh` execs `filebrowser "$@"`. Port 3002. |
| `git-sync` | `alpine:3.19` | Sidecar that clones `GIT_REPOSITORY_URL` and `git reset --hard`-syncs it into `SYNC_DIR` (default `/app/home/code`) every `SYNC_PERIOD` seconds. Configured entirely via env vars. |

### git-sync environment variables

`GIT_REPOSITORY_URL` (required), `GIT_REPOSITORY_BRANCH` (default `main`),
`GIT_REPOSITORY_USERNAME` / `GIT_REPOSITORY_PASSWORD` (optional, injected into
the HTTPS URL), `SYNC_DIR` (default `/app/home/code`), `SYNC_PERIOD` (default
`60`).

## Per-image layout convention

Every image directory follows the same shape:

- `Dockerfile` — the build recipe; declares `USER_UID`/`USER_GID`/`TARGETARCH`
  build args and switches to the non-root user before the entrypoint.
- `build.sh` — a thin wrapper over `tools/build-image.sh`; builds the image
  locally for testing (host arch, no push). The image's name/context lives in
  `.github/images.json`.
- `init.sh` / `entrypoint.sh` — the container entrypoint. Used by `python*`,
  `code-server`, `file-browser` and `git-sync`. `ttyd` and `vllm-openai` have
  no script.
- Bundled binaries / wrappers as needed: `ttyd.x86_64` (statically copied to
  `/usr/bin/ttyd`), `google-chrome` (a Chrome launcher wrapper with headless
  flags, installed over the real binary).

To add a new image: create `images/<name>/` with a `Dockerfile` and an
`init.sh`/`entrypoint.sh` if it needs startup logic, add a `build.sh` wrapper,
and register the image in `.github/images.json` (name, context, dockerfile,
watch path) so CI builds and publishes it. Keep the non-root
`appuser`/`USER_UID 999` pattern and the bundled-binary approach for
reproducibility.

## Build & publish

`.github/images.json` is the single source of truth for every image's name,
context and Dockerfile. CI publishes versioned `linux/amd64` images to quay.io
on release — see [`docs/release-process.md`](../../docs/release-process.md).

For local testing, run an image's `build.sh` (a wrapper over
`tools/build-image.sh`): it builds `quay.io/churrostack/<name>:0.0.1-local` for
the host arch and does **not** push. Note tag names do not always match the
directory name (e.g. `python/` → `python-streamlit`, `python-x11/` →
`python-streamlit-x11`); the mapping is defined in `.github/images.json`.

The Nx `docker-build` target tags with the bare `churrostack/...` prefix
(no registry, no push) and predates this setup — prefer `build.sh` or CI.

## Deprecated images

`images/_old/` contains retired images and is not built or published:

- `_old/base` — a Debian-based `churrostack/base:latest` bundling supervisor,
  X11/noVNC, Chrome, filebrowser and gotty.
- `_old/streamlit` — `churrostack/streamlit:latest`, built `FROM
  churrostack/base:latest`.

The current images replaced this `base`-layering model with self-contained,
single-purpose images. Do not extend or revive `_old/` — add new images at the
`images/` top level instead.
