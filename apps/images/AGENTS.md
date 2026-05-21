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
  subdirectory layout; prefer the per-image `build.sh` for correct tags.
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
- `build.sh` — a 1–2 line script: `docker build --platform linux/amd64` with
  the canonical tag, followed (for most images) by `docker push`.
- `init.sh` / `entrypoint.sh` — the container entrypoint. Used by `python*`,
  `code-server`, `file-browser` and `git-sync`. `ttyd` and `vllm-openai` have
  no script.
- Bundled binaries / wrappers as needed: `ttyd.x86_64` (statically copied to
  `/usr/bin/ttyd`), `google-chrome` (a Chrome launcher wrapper with headless
  flags, installed over the real binary).

To add a new image: create `images/<name>/` with a `Dockerfile`, a `build.sh`
following the tagging convention below, and an `init.sh`/`entrypoint.sh` if it
needs startup logic. Keep the non-root `appuser`/`USER_UID 999` pattern and
the bundled-binary approach for reproducibility.

## Build & publish

The authoritative way to build and publish is the per-image `build.sh`, run
from inside the image directory. Each script:

1. Builds for `linux/amd64` (`docker build --platform linux/amd64`).
2. Tags as `quay.io/churrostack/<name>:latest` — the registry is **quay.io**,
   not Docker Hub. Note tag names do not always match the directory name
   (e.g. `python/` → `quay.io/churrostack/python-streamlit:latest`,
   `python-x11/` → `python-streamlit-x11:latest`).
3. Pushes to quay.io (`docker push`).

The Nx `docker-build` target tags with the bare `churrostack/...` prefix
(no registry, no push) and is mainly useful for local image builds — the
canonical tags come from `build.sh`.

## Deprecated images

`images/_old/` contains retired images and is not built or published:

- `_old/base` — a Debian-based `churrostack/base:latest` bundling supervisor,
  X11/noVNC, Chrome, filebrowser and gotty.
- `_old/streamlit` — `churrostack/streamlit:latest`, built `FROM
  churrostack/base:latest`.

The current images replaced this `base`-layering model with self-contained,
single-purpose images. Do not extend or revive `_old/` — add new images at the
`images/` top level instead.
