# churrun-kubernetes — Agent Guide

The repository-wide agent guide at [`AGENTS.md`](../../AGENTS.md) (monorepo
root) takes precedence over this file — read it first.

## Overview

`churrun-kubernetes` is the ChurroStack **deployment runner for Kubernetes**: a
.NET 10 ASP.NET Core API that turns ChurroStack application/extension
*templates* into live Kubernetes workloads. It is the "churrun" backend that an
environment uses to deploy, start, stop, delete, monitor, and reverse-proxy
user applications onto a Kubernetes cluster.

Core responsibilities:

- Store reusable manifest **templates** (versioned by content hash).
- Render templates into Kubernetes manifests (`Deployment`, `Service`,
  `ConfigMap`, `PersistentVolumeClaim`, `Namespace`) and apply them to the
  cluster.
- Compose a base application template with **extension** templates (console,
  file-browser, storage, etc.) and resource-**size** patches.
- Watch the cluster for deployment status and events, scrape pod metrics, and
  stream pod logs back to callers (Server-Sent Events).
- Reverse-proxy inbound traffic to running workloads via YARP under
  `/share/{appName}/{portName}/...`.

## Tech stack

- **.NET 10** / `Microsoft.NET.Sdk.Web`, C#, nullable + implicit usings enabled.
- ASP.NET Core controllers + OpenAPI/Swagger UI (dev only).
- Notable NuGet packages (see `src/ChurrunKubernetes.csproj`):
  - `KubernetesClient` — official Kubernetes .NET client.
  - `DispatchR.Mediator` — in-process mediator (command/handler pattern).
  - `Scriban` — template rendering engine for manifest templates.
  - `YamlDotNet.System.Text.Json` — YAML <-> JSON for manifests/patches.
  - `JsonPatch.Net` + `Newtonsoft.Json` — JSON Patch / JSONPath manifest patching.
  - `Microsoft.EntityFrameworkCore` + `.Sqlite` — persistence.
  - `Mapster` (+ `.DependencyInjection`, `.EFCore`) — object mapping.
  - `Yarp.ReverseProxy` — dynamic reverse proxy to workloads.
  - `LazyCache.AspNetCore` — in-memory caching (environment info).
  - `IdGen` — distributed (snowflake-style) long ID generation.

## Commands

Run Nx targets from the repo root (targets defined in `project.json`):

- `pnpm nx build churrun-kubernetes` — `dotnet build ChurrunKubernetes.slnx`
- `pnpm nx serve churrun-kubernetes` — `dotnet run --project src/ChurrunKubernetes.csproj`
- `pnpm nx test churrun-kubernetes` — `dotnet test ChurrunKubernetes.slnx`
  (no test projects currently exist in the solution)
- `pnpm nx publish churrun-kubernetes` — `dotnet publish src/ChurrunKubernetes.csproj -c Release`

All Nx commands run with `cwd: apps/churrun-kubernetes`. Equivalent direct
`dotnet` commands must be run from `apps/churrun-kubernetes/` — note the
**solution file `ChurrunKubernetes.slnx` is at the project root**, while the
**`.csproj` lives under `src/`**.

EF Core migrations (run from `apps/churrun-kubernetes/src/`):

```
dotnet ef migrations add <Name>
dotnet ef database update
```

`launchSettings.json` profiles: `http` (localhost:5042), `https`
(localhost:7299), and `Container (Dockerfile)`.

## Architecture

### Entry point — `src/Program.cs`

Wires everything up: controllers (+ global `ResponseExceptionFilter` and a
plain-text `TextInputFormatter`), OpenAPI, EF Core (`ChurrunDbContext` on
SQLite), HMAC authentication, Mapster, DispatchR mediator, hosted background
jobs, and the YARP reverse proxy. On startup it runs `context.Database.Migrate()`
and `ProxyConfigurationProvider.Initialize()`. Kestrel/form limits are set to
~10 GB to allow large uploads. `app.Use` forces `Request.Scheme = "https"`.

### Command / handler pattern (DispatchR)

Each operation is a request class plus a handler, paired by filename
convention `Xxx.cs` / `Xxx.Handler.cs` under `src/Commands/<Area>/`. Handlers
implement `IRequestHandler<TRequest, TResponse>`; controllers do almost no
logic and just `_mediator.Send(...)`. Handlers can call other handlers via the
mediator (e.g. `CreateDeployment` sends `EnsureEnvironmentHasQuota`,
`GetTemplate`, `GetEnvironment`). Handlers are auto-registered from the
assembly (`AddDispatchR`).

### Kubernetes integration — `src/Services/KubernetesService.cs`

Singleton wrapping the `KubernetesClient`. Connection: if
`Kubernetes:Connection:ClientCertificateKeyData` is set it builds an explicit
`KubernetesClientConfiguration` (host + client cert); otherwise it falls back
to `InClusterConfig()` (service-account, expected in-cluster). Key operations:

- `ApplyYamlManifests` — splits a multi-doc YAML string on `---`, deserializes
  each by `Kind`, stamps `churrostack.com/*` annotations, and creates the
  resource; on `409 Conflict` it `Replace`s (or for PVCs `MergePatch`es the
  storage request). Supported kinds: `Namespace`, `Deployment`, `Service`,
  `ConfigMap`, `PersistentVolumeClaim`. Other kinds throw `NotSupportedException`.
- `Get*Manifests` — list resources filtered by `churrostack.com/*` annotations.
- `Delete*Manifest`, `ScaleManifestAsync` — delete / scale workloads.
- `MonitorDeploymentAsync` / `MonitorEventsAsync` — `Watch`-based async
  streams of deployment status and namespaced events.
- `ScrapeMetricsAsync`, `MonitorLogsAsync` — pod metrics and followed logs.

### Workload model & annotations

A workload is identified by ChurroStack annotations stamped on every manifest:
`churrostack.com/deployment-id`, `app-id`, `app-hash`, `template-id`,
`template-hash`. Start/Stop/Delete commands locate cluster resources by these
annotations rather than by resource name. The `Deployment` domain entity
(`src/Domain/Deployment.cs`) persists only proxy-relevant data: `Name`
(deployment id, PK), `AppName`, `Size`, and `Ports`.

### Manifest templating — `src/Services/TemplateService.cs`

Templates are Scriban templates that produce YAML manifests.

- `EvaluateAsync` — runs a template and returns its `template` script variable
  as JSON (the template *definition*: `type`, `name`, `extensions`, `ports`).
  Template `type` must be `application`, `dependency`, or `extension`.
- `TransformAsync` — renders a template with supplied args (id, namespace,
  basePath, parameters, variables, environment, etc.) into final YAML.
- `PatchYamlAsync` — applies extension/size patches: each patch targets
  manifests by `kind`/`group`/`name` and runs JSON Patch ops or custom
  `jsonpath`-typed `add`/`replace` ops.
- Custom Scriban helpers: `get_config`, `has_config`, `trim`, `trim_start`,
  `trim_end`. The context is relaxed (non-strict variables, null indexer).

`CreateDeployment.Handler` is the orchestrator: resolves the base template +
required/requested extensions, renders the base manifest, renders each
extension into a patch, renders `Resources/size.yaml` into a size patch,
merges them with `PatchYamlAsync`, applies the result, persists the
`Deployment`, and registers proxy routes.

### Event monitoring — `src/Jobs/` + `src/Services/State/`

Two `BackgroundService`s — `KubernetesEventsMonitoringJob` and
`KubernetesDeploymentsMonitoringJob` — continuously watch the configured
namespace and push items into singleton `EventsStateService<T>` instances
(backed by a TPL Dataflow `BufferBlock`). On error they log and retry after
5 s. `MonitoringController` drains those buffers and streams updates to
clients as Server-Sent Events (`text/event-stream`) at `GET /api/monitoring/events`
and `/api/monitoring/state`; it also exposes `/metrics` and `/console/{appName}`
(streamed pod logs). `PeriodicJobService<T>` is a generic timer-based base
class for periodic jobs.

### Reverse proxy — `src/Services/Share/`

`ProxyConfigurationProvider` (a YARP `IProxyConfigProvider`) builds routes
`/share/{appName}/{portName}/{**catch-all}` -> cluster destinations
(`http://{deploymentName}:{port}`), all protected by the `HmacScheme`
authorization policy. `DestinationSelectionPolicy` is a custom YARP load
balancer (`DestinationPolicy`) that honors an `X-Destination-Id` header to
pin a request to a specific pod/deployment. Proxy config is rebuilt on
deployment create/delete and on startup; YARP request transforms strip
internal headers (`Authorization`, `X-Signature`, `X-Environment-Name`, etc.)
before forwarding.

### Authentication — `src/Services/HmacAuthenticationHandler.cs`

The single auth scheme `HmacScheme`. Requests must send `X-Environment-Name`,
`X-Signature`, and `X-Timestamp`; the handler recomputes an HMAC-SHA256 over a
canonical URL + environment name + timestamp using the base64
`EncryptionKey` config value and compares signatures. All controllers are
`[Authorize]`.

### Data layer — `src/Data/`

`ChurrunDbContext` (EF Core, SQLite) applies `IEntityTypeConfiguration`s from
the assembly. Two tables (`src/Migrations/`):

- `Deployment` — PK `Name`; `Size` and `Ports` stored as JSON `TEXT` via value
  converters.
- `Template` — autoincrement `Id` PK, unique index on `(Name, Hash)`; `Content`
  holds the raw Scriban template, `Hash` is the SHA-1 of the content (templates
  are immutable and content-versioned).

## Directory structure

```
apps/churrun-kubernetes/
  ChurrunKubernetes.slnx      Solution (project root)
  project.json                Nx targets
  README.md                   Deployment API example
  src/
    Program.cs                Entry point / DI / pipeline
    appsettings.json          Configuration
    JsonSettings.cs           Shared System.Text.Json options (camelCase)
    Dockerfile, build.sh      Container image (build.sh = local build helper)
    sizes.yaml                Available deployment sizes (env info)
    Controllers/              Thin HTTP layer (Deployments, Templates,
                              Environment, Monitoring)
    Commands/<Area>/          DispatchR requests + .Handler.cs handlers
    Services/                 Kubernetes, Template, IdGeneration, HMAC auth;
                              Share/ (YARP proxy), State/ (event buffers)
    Jobs/                     Background watch jobs
    Domain/                   EF Core entities (Deployment, Template)
    Data/                     DbContext + entity Configuration/
    Migrations/               EF Core SQLite migrations
    Mappers/                  Mapster IRegister mappings
    Middlewares/              ResponseExceptionFilter (global exception -> HTTP)
    Models/                   Dtos/ (request/response), Logs/ (k8s events,
                              metrics), Proxy/
    Resources/                Scriban templates copied to output
    Utils/                    NamingUtils, Base36, JSON converters, extensions
```

## Configuration — `src/appsettings.json`

Bound directly via `IConfiguration` (no strongly-typed options class). Note:
`appsettings.json` contains JSON comments. Key settings (override with user
secrets / env vars — `UserSecretsId` is set on the project):

- `Name` — environment name (required; used in environment info).
- `EncryptionKey` — base64 HMAC key for request authentication.
- `ConnectionStrings:Database` — SQLite connection string (required).
- `Kubernetes:Namespace` — target namespace for all workloads/watches.
- `Kubernetes:ServiceMesh` — `null` or `istio`.
- `Kubernetes:StorageClass` / `SharedStorageClass` — storage class names
  (default `hostPath` when unset).
- `Kubernetes:Limits:{Cpu,Memory,Storage,Gpu}` — environment resource quota,
  enforced by `EnsureEnvironmentHasQuota`.
- `Kubernetes:Connection:{Host,SkipTlsVerify,ClientCertificateData,
  ClientCertificateKeyData}` — explicit cluster connection; if
  `ClientCertificateKeyData` is empty the app uses in-cluster config.
- `Kubernetes:DebugDestinationHost` — DEBUG-only override of proxy target host.

### Resource & size files

- `src/sizes.yaml` — the catalog of deployment **sizes** (cpu/memory/gpu
  requests & limits) returned by `GetEnvironment`. Read from `/app/sizes.yaml`
  in Release, `./sizes.yaml` in DEBUG; copied to output via the `.csproj`.
- `src/Resources/size.yaml` — Scriban patch template that injects a chosen
  size's `resources` block into a deployment's container.
- `src/Resources/environment.yaml` — Scriban template describing an
  environment (sizes, limits, capabilities) sourced from `Kubernetes:*` config.

## Database

EF Core with **SQLite** (`Microsoft.EntityFrameworkCore.Sqlite`). Migrations
live in `src/Migrations/`; `Program.cs` runs `Database.Migrate()` on startup,
so a fresh DB is created and upgraded automatically. DEBUG builds enable
sensitive-data logging and detailed errors.

## Deployment

`src/Dockerfile` — multi-stage build on `mcr.microsoft.com/dotnet/sdk:10.0`
(build) and `aspnet:10.0` (runtime), runs as the non-root `$APP_UID`, exposes
8080/8081, entrypoint `dotnet ChurrunKubernetes.dll`. **Build context is
`src/`** (the Dockerfile `COPY`s `ChurrunKubernetes.csproj` from the context
root). `src/build.sh` builds a local test image
(`quay.io/churrostack/churrun-kubernetes:0.0.1-local`, host arch, no push); CI
publishes versioned `linux/amd64` images on release — see
[`docs/release-process.md`](../../docs/release-process.md).

## Conventions & gotchas

- **One request + one handler per file pair**: `Xxx.cs` and `Xxx.Handler.cs`
  under `Commands/<Area>/`. Keep controllers thin — delegate to the mediator.
- Templates are **content-addressed**: a template version is identified by
  `name/{base64(SHA-1 hash)}`; creating an identical template is idempotent.
- All cluster lookups go through `churrostack.com/*` **annotations**, not
  resource names — preserve these annotations when adding manifest kinds.
- When `ApplyYamlManifests` needs a new resource kind, add a `case` for it
  (create + 409-conflict fallback) or it will throw `NotSupportedException`.
- Names (deployment, extension, port) must match `^[a-z0-9-]*$` and not start
  with `_` or contain `__`; template names additionally allow `.` and `_`
  (`NamingUtils`).
- Use the shared `JsonSettings.Value` / `ApplyDefaultOptions()` for JSON
  serialization to stay consistent (camelCase, ignore-null).
- `IdGenerationService` derives the worker id from the last hyphen-segment of
  the hostname — it assumes a Kubernetes pod naming pattern.
- Exceptions are translated to HTTP status by `ResponseExceptionFilter`:
  `HttpException` -> its code, `InvalidOperationException` -> 404,
  `ArgumentException` -> 400, `UnauthorizedAccessException` -> 403, else 500.
- `CreateDeployment` supports a `?dry=true` query param to render manifests
  without applying them or persisting state.
- Monitoring endpoints return SSE streams, not JSON bodies — clients must read
  `text/event-stream`.
