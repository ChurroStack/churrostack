# churrun-tunnel — Agent Guide

The repository-wide agent guide at [`AGENTS.md`](../../AGENTS.md) (monorepo
root) takes precedence over this file — read it first.

## Overview

`churrun-tunnel` is the reverse-SSH-tunnel **client**. It runs as a container on
a customer's private / on-premises server and dials out to the ChurroStack
[`tunnel-server`](../tunnel-server) to open a reverse tunnel. This lets
ChurroStack reach a service on the private server without that server exposing
any inbound ports to the internet — the connection is always outbound from the
private side.

It is a Bash/Alpine project (`stack:bash` tag in `project.json`), distributed as
the Docker image `quay.io/churrostack/churrun-tunnel:latest`.

## How it works

The actual tunnel is established by **`entrypoint.sh`**, the container's
entrypoint. It:

1. Validates required env vars (`SSH_HOST`, `SSH_USER`, `REMOTE_PORT`,
   `LOCAL_PORT`) and fails fast if any are missing.
2. Optionally writes `SSH_KNOWN_HOSTS` to `/home/tunnel/.ssh/known_hosts`.
3. Requires an SSH private key mounted at `/home/tunnel/.ssh/keys/id_ed25519`;
   exits with an error if absent.
4. `exec`s `autossh` to open a persistent reverse forward:
   `-R ${REMOTE_PORT}:${LOCAL_HOST}:${LOCAL_PORT}` to
   `${SSH_USER}@${SSH_HOST}:${SSH_PORT}`.

`autossh` is run with `-M 0` (monitoring disabled — it relies on SSH keepalives
instead) and `-N` (no remote command). SSH options enforce
`ExitOnForwardFailure=yes`, `StrictHostKeyChecking=yes`, `IdentitiesOnly=yes`,
and `ServerAliveInterval=30` / `ServerAliveCountMax=3`. If SSH dies, `autossh`
re-establishes it; its restart timing is governed by the `AUTOSSH_*` env vars.

Net effect: a connection to `REMOTE_PORT` on the `tunnel-server` is forwarded
back through the SSH connection to `LOCAL_HOST:LOCAL_PORT` on the private server.

## Commands

Nx target (from the monorepo root):

```
pnpm nx run churrun-tunnel:run
```

This runs `bash churrun` in `apps/churrun-tunnel` — see the Files section; in the
current tree `churrun` is a placeholder, so this prints a stub message and
exits. The real workload runs via Docker, not this Nx target.

Build the Docker image locally for testing (from `apps/churrun-tunnel`):

```
bash build.sh
```

`build.sh` delegates to `tools/build-image.sh` and builds
`quay.io/churrostack/churrun-tunnel:0.0.1-local` for the host architecture
without pushing. Versioned `linux/amd64` images are built and pushed by CI on
release — see [`docs/release-process.md`](../../docs/release-process.md).

## Files

- **`churrun`** — Nx `run` target script. **Currently a placeholder stub**: it
  prints `churrun-tunnel: placeholder stub — copy the real client script here.`
  and exits. It is not wired into the Docker image. Treat it as a TODO until a
  real client script replaces it.
- **`entrypoint.sh`** — the real client logic and the container `ENTRYPOINT`.
  Validates env vars and launches `autossh` (see How it works).
- **`build.sh`** — thin wrapper over `tools/build-image.sh`; builds a local
  test image (no push). CI handles versioned publishing.
- **`Dockerfile`** — Alpine 3.19 image; installs `openssh-client`, `autossh`,
  `bash`, `ca-certificates`; creates an unprivileged `tunnel` user (uid 10001)
  and runs as that user; sets up `~/.ssh` and `~/.ssh/keys` with `0700`; copies
  in `entrypoint.sh`.
- **`project.json`** — Nx project config; defines only the `run` target.

## Configuration

All configuration is via environment variables read by `entrypoint.sh`.

Required (no defaults — container exits if unset):

| Var | Meaning |
| --- | --- |
| `SSH_HOST` | Hostname/IP of the ChurroStack `tunnel-server`. |
| `SSH_USER` | SSH user on the tunnel-server. |
| `REMOTE_PORT` | Port opened on the tunnel-server side of the reverse tunnel. |
| `LOCAL_PORT` | Local port on the private server that the tunnel forwards to. |

Optional:

| Var | Default | Meaning |
| --- | --- | --- |
| `SSH_PORT` | `22` (Dockerfile `ENV`) | SSH port on the tunnel-server. |
| `LOCAL_HOST` | `127.0.0.1` (Dockerfile `ENV`) | Host the tunnel forwards to on the private side. |
| `SSH_KNOWN_HOSTS` | unset | If set, written verbatim to `~/.ssh/known_hosts`. |
| `AUTOSSH_LOGFILE` | `/dev/stdout` (Dockerfile `ENV`) | autossh log destination. |
| `AUTOSSH_GATETIME` | `0` (Dockerfile `ENV`) | Seconds a session must survive to be "good"; `0` disables the gate so quick failures still restart. |
| `AUTOSSH_POLL` | `30` (Dockerfile `ENV`) | autossh poll interval (seconds). |
| `AUTOSSH_FIRST_POLL` | `30` (Dockerfile `ENV`) | First autossh poll interval (seconds). |

Mounted file (not an env var) — **required**:

- `/home/tunnel/.ssh/keys/id_ed25519` — the SSH private key. `entrypoint.sh`
  exits with an error if it is missing.

## Deployment

The container runs on the customer's private server:

1. Use a CI-published image `quay.io/churrostack/churrun-tunnel:<version>` (or
   `:latest`). For local testing, `bash build.sh` builds `:0.0.1-local`.
2. On the private server, run the image with the required env vars set and the
   private key mounted, e.g.:

   ```
   docker run -d --restart unless-stopped \
     -e SSH_HOST=tunnel.churrostack.example \
     -e SSH_USER=tunnel \
     -e REMOTE_PORT=20001 \
     -e LOCAL_PORT=8080 \
     -e SSH_KNOWN_HOSTS="$(cat known_hosts)" \
     -v /path/to/id_ed25519:/home/tunnel/.ssh/keys/id_ed25519:ro \
     quay.io/churrostack/churrun-tunnel:latest
   ```

3. The corresponding public key must be authorized on the `tunnel-server`.

## Conventions / gotchas

- **`churrun` is a placeholder** — do not assume it is the production client.
  The container runs `entrypoint.sh`, not `churrun`.
- The container runs as the **non-root** `tunnel` user (uid 10001). Mounted key
  files and `~/.ssh` paths must be readable by that uid.
- `StrictHostKeyChecking=yes` means the tunnel-server host key **must** be known
  ahead of time — provide it via `SSH_KNOWN_HOSTS`, otherwise the connection
  fails on first connect.
- `IdentitiesOnly=yes` + `-i` means only the mounted `id_ed25519` key is used;
  no agent or other keys.
- `autossh` uses `-M 0`; liveness depends entirely on SSH `ServerAlive*`
  keepalives, not autossh's own monitoring port.
- `ExitOnForwardFailure=yes`: if the reverse port cannot be bound on the
  tunnel-server (e.g. `REMOTE_PORT` already in use), SSH exits and autossh
  retries rather than running a tunnel-less connection.
