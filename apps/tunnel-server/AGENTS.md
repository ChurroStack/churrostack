# tunnel-server — Agent Guide

The repository-wide agent guide at [`AGENTS.md`](../../AGENTS.md) (monorepo
root) takes precedence over this file — read it first.

## Overview

`tunnel-server` exposes private/internal HTTP apps to the public internet
through SSH reverse tunnels — without those private servers needing inbound
firewall rules or public IPs.

A private server opens an outbound SSH connection to the SSH container and
asks it to listen on a port locally on the server side (`ssh -R`). Public
HTTP traffic then arrives at the YARP proxy, which forwards each request to
the correct tunneled local port. Because the SSH connection is outbound from
the private server, NAT/firewalls on that side are never a problem.

The project ships as **two cooperating containers**, both built and pushed to
`quay.io/churrostack/`.

## Components

### `ChurrOS.TunnelService` — .NET YARP reverse proxy

`src/ChurrOS.TunnelService/` — an ASP.NET Core (`Microsoft.NET.Sdk.Web`)
service whose entire job is in `Program.cs`. It configures a single in-memory
YARP route (`{**catch-all}`) and rewrites each request's destination at
runtime from request headers:

- `X-Port` (**required**) — the local port the request should be proxied to.
  Missing header → `400 "X-Port header not found."`
- `X-Schema` (optional, default `http`) — must be `http` or `https`; any
  other value → `400 "Invalid X-Schema."`

The request is then forwarded to `{schema}://localhost:{port}` plus the
original path and query string. The route's cluster has a placeholder
destination (`http://localhost:0/`) that is always overridden by the
transform.

Other behavior set in `Program.cs`:
- Kestrel `MaxRequestBodySize` and multipart `MultipartBodyLengthLimit` are
  raised to `10_000_000_000` (~10 GB); the route sets `MaxRequestBodySize =
  -1` (unlimited) so large uploads pass through.
- `app.UseHttpsRedirection()` is enabled.
- Health endpoint `GET /health` (registered `self` check, always Healthy).

The tunneled apps' ports must be reachable on `localhost` from this service —
in deployment the SSH container provides those listeners, so this proxy and
the SSH container share a network/host such that `localhost:{port}` resolves
to the SSH-side tunnel listeners.

### `ChurrOS.Ssh` — OpenSSH reverse-tunnel container

`src/ChurrOS.Ssh/` — an Alpine-based OpenSSH server (no .NET) that terminates
the reverse SSH tunnels from private servers. It is locked down to do nothing
but port forwarding:

- `sshd_config`: listens on **port 8443**, pubkey-only auth, no shells/PTY,
  no agent/X11 forwarding, `AllowTcpForwarding remote`, `GatewayPorts yes`,
  single `tunnel` user, `ForceCommand` rejects any interactive use.
- **Dynamic key authorization via Postgres.** `AuthorizedKeysFile` is `none`;
  instead `AuthorizedKeysCommand` runs `reverse_tunnel_key_lookup.sh`, which
  queries `cs.environment` for a row whose `ssh_public_key` matches the
  presented key and returns that key restricted with
  `permitlisten="localhost:{port}"` — so each registered environment can only
  bind its own assigned port.
- `entrypoint.sh` generates host keys on first run, persists the `PG*` env
  vars to `/run/authorized_keys_env` (for the AuthorizedKeysCommand) and
  writes `~/.pgpass`.

## Tech stack

- .NET 10 / ASP.NET Core (`net10.0`, `Microsoft.NET.Sdk.Web`), `Nullable`
  and `ImplicitUsings` enabled.
- `Yarp.ReverseProxy` 2.3.0 — the reverse proxy engine.
- `Microsoft.VisualStudio.Azure.Containers.Tools.Targets` 1.23.0 — Docker
  tooling support.
- `ChurrOS.Ssh`: Alpine 3.19, OpenSSH, `tini`, `postgresql-client`.
- Solution: `src/ChurrOS.TunnelService.slnx` (contains only the
  `ChurrOS.TunnelService` project; `ChurrOS.Ssh` is not a .NET project).

## Commands

Run Nx targets from the repo root (defined in `project.json`):

| Task | Nx command | Underlying command |
|------|-----------|--------------------|
| Build | `pnpm nx build tunnel-server` | `dotnet build src/ChurrOS.TunnelService.slnx` |
| Run | `pnpm nx serve tunnel-server` | `dotnet run --project src/ChurrOS.TunnelService/ChurrOS.TunnelService.csproj` |
| Test | `pnpm nx test tunnel-server` | `dotnet test src/ChurrOS.TunnelService.slnx` |
| Publish | `pnpm nx publish tunnel-server` | `dotnet publish src/ChurrOS.TunnelService/ChurrOS.TunnelService.csproj -c Release` |

Note: there is currently no test project in the solution, so `test` is a
no-op until one is added.

Build/push container images (run from `apps/tunnel-server/src/`):
- `./build.sh` — builds and pushes the proxy image
  `quay.io/churrostack/churros-tunnel-proxy:latest`
  (`-f ./ChurrOS.TunnelService/Dockerfile`, platform `linux/amd64`).
- `./ChurrOS.Ssh/build.sh` — builds and pushes the SSH image
  `quay.io/churrostack/churros-tunnel:latest` (run from inside `ChurrOS.Ssh/`).

## Request / traffic flow

1. A private server runs `ssh -N -R` to the `ChurrOS.Ssh` container on port
   `8443`, authenticating with a public key registered in `cs.environment`.
2. `sshd` authorizes the key (Postgres lookup) and — restricted by
   `permitlisten` — lets that connection open a listener on its assigned
   `localhost:{port}`. Traffic to that port is tunneled back to the private
   server's local app.
3. An external HTTP request reaches `ChurrOS.TunnelService` (the YARP proxy)
   carrying `X-Port` (and optionally `X-Schema`). An upstream layer/router is
   responsible for setting these headers per environment.
4. The YARP request transform rewrites the destination to
   `{schema}://localhost:{X-Port}` + path + query and forwards the request,
   which lands on the SSH tunnel listener and is delivered to the private
   app. The response flows back the same path.

## Directory structure

```
apps/tunnel-server/
├── project.json                  # Nx targets
├── README.md, LICENSE
├── AGENTS.md  (CLAUDE.md → symlink to this file)
└── src/
    ├── ChurrOS.TunnelService.slnx
    ├── build.sh                  # build+push proxy image
    ├── .dockerignore
    ├── ChurrOS.TunnelService/    # .NET YARP proxy
    │   ├── Program.cs            # all proxy logic + header transform
    │   ├── ChurrOS.TunnelService.csproj
    │   ├── appsettings.json
    │   ├── Dockerfile
    │   └── Properties/launchSettings.json
    └── ChurrOS.Ssh/              # OpenSSH reverse-tunnel container
        ├── Dockerfile
        ├── sshd_config
        ├── entrypoint.sh
        ├── reverse_tunnel_key_lookup.sh
        └── build.sh
```

Entry points: `ChurrOS.TunnelService/Program.cs` (proxy);
`ChurrOS.Ssh/entrypoint.sh` → `sshd` (SSH container).

## Configuration

**`ChurrOS.TunnelService`**
- `appsettings.json`: only logging levels and `AllowedHosts`. There are no
  custom config keys — routing is fully header-driven, not config-driven.
- `launchSettings.json` profiles: `http` (`http://localhost:5220`), `https`
  (`https://localhost:7089;http://localhost:5220`), `Container (Dockerfile)`
  (`ASPNETCORE_HTTP_PORTS=8080`, `ASPNETCORE_HTTPS_PORTS=8081`).
- Container exposes `8080`/`8081`.
- Per-request headers: `X-Port` (required), `X-Schema` (`http`/`https`).

**`ChurrOS.Ssh`**
- SSH listens on `8443` (exposed).
- Env vars consumed by `entrypoint.sh` / key lookup:
  `PGHOST` (default `localhost`), `PGPORT` (`5432`), `PGUSER` (`tunnel`),
  `PGDATABASE` (`ChurrOS`), `PGPASSWORD`. These point at the Postgres
  instance holding the `cs.environment` table.

## Deployment

Two images, both `linux/amd64`, pushed to Quay:
- `ChurrOS.TunnelService/Dockerfile` — multi-stage (`dotnet/sdk:10.0` build →
  `dotnet/aspnet:10.0` runtime), runs as non-root `$APP_UID`,
  `ENTRYPOINT dotnet ChurrOS.TunnelService.dll`. Build context is `src/`.
- `ChurrOS.Ssh/Dockerfile` — Alpine + OpenSSH, runs via `tini`,
  `CMD sshd -D -e`. Uses `setcap` to grant `sshd` the capabilities to bind
  ports and drop privileges instead of running the daemon as root.

## Conventions & gotchas

- **Routing is entirely header-driven.** YARP loads one in-memory
  catch-all route; the real destination is computed per request in the
  `AddRequestTransform`. There is no static cluster config to edit — change
  routing logic in `Program.cs`.
- The transform **throws `ArgumentException`** after writing the `400`
  response on bad/missing headers; this is intentional flow control to abort
  proxying, not a bug to "fix".
- The proxy assumes tunneled ports are reachable on `localhost`. The proxy
  container and the SSH container must therefore share a network namespace
  (or run on the same host) so `localhost:{port}` hits the SSH-side tunnel
  listeners.
- Body-size limits are deliberately ~10 GB across Kestrel, form options, and
  the route — preserve them if editing `Program.cs` (needed for large
  uploads through the tunnel).
- The SSH container is intentionally crippled: pubkey-only, no shell, port
  forwarding only, and `permitlisten` scoped per environment. Don't loosen
  `sshd_config` without understanding the security model.
- An environment's allowed listen port comes from the `port` column of
  `cs.environment` (queried in `reverse_tunnel_key_lookup.sh`) — that DB row
  is the source of truth for both which keys may connect and which port each
  may bind.
