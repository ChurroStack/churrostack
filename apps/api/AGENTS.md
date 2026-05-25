# api — Agent Guide

The repository-wide agent guide at [`AGENTS.md`](../../AGENTS.md) (monorepo
root) takes precedence over this file — read it first.

## Concepts

Before touching resource/quota code paths, read the canonical vocabulary in
[`docs/concepts/environment-resources.md`](../../docs/concepts/environment-resources.md)
— covers Used / Requested / Allocated / Quota, where each value is computed
(`GetEnvironmentTotals`, `ScrapeMetricsJob`), and how runtime CPU/Memory quota
is enforced (`EnsureEnvironmentRunningQuota` + per-env Redis lock from
`StartApplicationHandler` / `UpdateApplicationHandler`).

Before touching the `/share/*` YARP pipeline or `Start`/`Stop` handlers, read
[`docs/concepts/auto-start-stop.md`](../../docs/concepts/auto-start-stop.md) —
covers the Auto-Start hold path, single-flight `SETNX`, the route cache
contract, Auto-Stop idle detection (combined HTTP-request + CPU signal), and
the cooldown semantics.

## Overview

`apps/api` is the ChurroStack backend: a C# .NET 10 ASP.NET Core service named
**ChurrOS**. It is a multi-tenant control plane that manages *accounts*,
*environments* (remote compute hosts), *applications* and their *deployments*,
*identities*, *templates*, *metrics* and *traces*. It also acts as an OAuth 2.0
/ OpenID Connect identity provider, and as a reverse proxy that signs and
forwards `/share/*` traffic to per-tenant environments through a tunnel.

The solution `src/ChurrOS.slnx` contains three projects:

- **ChurrOS.Api** — the ASP.NET Core API (all business logic).
- **ChurrOS.AppHost** — a .NET Aspire orchestrator that runs the API plus a
  YARP `ingress` gateway for local development.
- **ChurrOS.ServiceDefaults** — shared Aspire defaults (OpenTelemetry, health
  checks, HTTP resilience, service discovery), referenced by the other two.

## Tech stack

- **.NET 10** (`net10.0`), ASP.NET Core, controllers + `IMediator`.
- **.NET Aspire 9.5** — `Aspire.AppHost.Sdk`; AppHost orchestrates the API and
  a YARP ingress gateway.
- **EF Core 10** + **Npgsql** (PostgreSQL provider), `EFCore.NamingConventions`
  (snake_case). The DB also needs the **TimescaleDB** extension (hypertables).
- **DispatchR.Mediator** (`DispatchR.Mediator`) — the mediator for the
  command/handler pattern (not MediatR).
- **OpenIddict 7.2** (`AddCore`/`AddServer`/`AddValidation`) — OAuth2/OIDC
  server, plus ASP.NET Core Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`).
- **Quartz.NET 3.15** — background jobs with a clustered PostgreSQL persistent
  store; OpenIddict's token pruning runs via `OpenIddict.Quartz`.
- **YARP 2.3** (`Yarp.ReverseProxy`) — dynamic reverse proxy for `/share/*`.
- **SignalR** + Redis backplane (`SignalR.StackExchangeRedis`) — real-time
  client notifications via `NotificationHub`.
- **Redis** (`StackExchange.Redis`) — distributed cache, data-protection key
  ring, queues (Redis streams), SignalR backplane.
- **Mapster** (`Mapster.DependencyInjection`, `Mapster.EFCore`) — DTO mapping.
- **LazyCache** — in-process cache (tenant/identity resolution).
- Other: `Swashbuckle` (Swagger), `ClosedXML` (xlsx import/export), `Scriban` +
  `YamlDotNet` (template rendering), `LibGit2Sharp` + `SSH.NET` (Git/SSH),
  `IdGen` + `UUIDNext` (ID generation), `BouncyCastle`, `System.CommandLine`.

## Commands

Run Nx targets from the **repo root**; the underlying `dotnet` commands run
from `apps/api` (see `project.json`).

| Task | Nx | Direct (`cwd: apps/api`) |
|------|----|--------------------------|
| Build | `pnpm nx build api` | `dotnet build src/ChurrOS.slnx` |
| Run | `pnpm nx serve api` | `dotnet run --project src/ChurrOS.AppHost/ChurrOS.AppHost.csproj` |
| Test | `pnpm nx test api` | `dotnet test src/ChurrOS.slnx` |
| Publish | `pnpm nx publish api` | `dotnet publish src/ChurrOS.Api/ChurrOS.Api.csproj -c Release` |

`serve` launches the **Aspire AppHost**, not the API directly — that starts the
API and the YARP `ingress` gateway (HTTP `8000`, HTTPS `8001`) and opens the
Aspire dashboard. There is currently no test project in the solution, so `test`
is a no-op until one is added.

Other useful direct commands (from `apps/api/src/ChurrOS.Api`):

- `dotnet ef migrations add <Name>` — add an EF Core migration.
- `dotnet ef database update` — apply migrations manually (normally automatic;
  see Database below).
- `src/build.sh` — builds a local test image
  `quay.io/churrostack/churros-api:0.0.1-local` (host arch, no push). It
  delegates to `tools/build-image.sh`; CI publishes versioned images — see
  [`docs/release-process.md`](../../docs/release-process.md).

## Architecture

### Request flow

`Controller` → `IMediator.Send(command)` → `Handler`. Controllers are thin:
they construct a command object and return its result. Example:
`AccountController` → `GetAccount` → `GetAccountHandler`.

Middleware/pipeline order in `Program.cs`: forwarded headers → forced
`https` scheme → request localization → CORS (`Default`) → WebSockets →
`UseAuthentication` → **`MultiTenantMiddleware`** → routing → authorization →
SignalR hub (`/api/notifications`) → controllers → per-request metric
collection → YARP reverse proxy.

### Command / handler pattern (DispatchR)

Commands live in `Commands/<Feature>/` as **paired files**:

- `Xxx.cs` — the request: `public class Xxx : IRequest<Xxx, ValueTask<TResult>>`.
- `Xxx.Handler.cs` — `public class XxxHandler : IRequestHandler<Xxx, ValueTask<TResult>>`
  with an `async ValueTask<TResult> Handle(Xxx request, CancellationToken ct)`.

DispatchR is registered with `AddDispatchR(cfg => cfg.Assemblies.Add(typeof(Program).Assembly))`,
so handlers are auto-discovered. Handlers may call `_mediator.Send(...)` for
sub-commands (e.g. `EnsureHasRole`).

### Data layer

`ChurrosDbContext` extends `IdentityDbContext` with the OpenIddict entity set
(`OpenIdApplication/Authorization/Scope/Token`, keyed by `Guid`). Key points:

- **Multi-tenant global query filters**: `OnModelCreating` adds
  `HasQueryFilter(e => e.AccountId == AccountId)` to most entities. `AccountId`
  comes from `ITenantResolver`; queries are automatically tenant-scoped.
- Entity mappings live in `Data/Configuration/*Configuration.cs`
  (`IEntityTypeConfiguration`), applied via `ApplyConfigurationsFromAssembly`.
- snake_case naming via `UseSnakeCaseNamingConvention()`. Tables are in the
  `cs` schema (e.g. `cs.identity`, `cs.environment`).
- `ChurrosDbContextFactory` is the design-time factory for `dotnet ef`; it
  reads `appsettings.json` + `appsettings.Development.json` and passes `null`
  tenant/cache services — **design-time only**.
- The context exposes raw-SQL helpers (`ExecuteScalarAsync`,
  `ExecuteQueryAsync`) and tenant properties (`IdentityId`,
  `AccountEncryptionKey`, `Quota`, `Domains`).

### Auth (OpenIddict + Identity)

OpenIddict runs as an embedded OAuth2/OIDC **server**, **validation** consumer
(`UseLocalServer`), and **core** EF store. Enabled flows: authorization code +
PKCE, refresh token, client credentials, token exchange. Endpoints:
`oauth/token`, `oauth/authorize`, `oauth/logout`, `oauth/userinfo`. The
`api` client is a confidential client also used for the OIDC cookie login flow
(`/login/*`). Optional external login: Microsoft Account (enabled only when
`ExternalProviders:Microsoft:ClientId` is set).

Authentication schemes: OpenIddict JWT validation (default), a custom
**API-key** scheme (`ApiKeyAuthenticationHandler`), cookie, and OIDC.
Named authorization policies (in `Program.cs`): `JwtOrApiKeyPolicy`,
`ApiKeyPolicy`, `JwtPolicy`, `CookiePolicy`, `AppJwtPolicy`, `AppCookiePolicy`
(the last two add `ApplicationMemberAccessRequirement`, enforced by
`ApplicationMemberAccessHandler`).

### Background jobs (Quartz)

Jobs in `Jobs/` implement `IJob` (most are `[DisallowConcurrentExecution]`).
Quartz uses a **clustered persistent store on PostgreSQL** (same DB as the
app). Jobs are scheduled dynamically with `JobBuilder`/`TriggerBuilder` and
carry per-tenant data in `JobDataMap` (e.g. `accountId`, `environmentId`).
`QuotaResetJob` is scheduled in `MigrationExtension`. `ScrapeMetricsJob`,
`ScrapeDeploymentStateJob`, `ScrapeGenericEventsJob`, `ApplicationHttpRequestJob`
are scheduled by feature code. `TracesProcessorJob` is also registered as a
plain `IHostedService`.

### Real-time

`NotificationHub` (`Middlewares/NotificationHub.cs`) is mapped at
`/api/notifications`. `ClientNotificationService` pushes change events to
clients; the Redis backplane lets it scale across instances.

### Reverse proxy (YARP)

`ProxyConfigurationProvider` is a dynamic `IProxyConfigProvider` built from the
database (initialized at startup, refreshable). The `MapReverseProxy` pipeline
in `Program.cs` resolves the tenant for the matched cluster, enforces network
quota (`429` on exhaustion), and signs forwarded requests: it adds
`X-Environment-Name`, `X-Timestamp`, and an HMAC-SHA256 `X-Signature` header
(plus `X-Port`, `X-User-Id`, `X-Destination-Id` for `/share/*` workspace
routes). The AppHost's separate `ingress` YARP only does local path routing.

## Directory structure

`src/ChurrOS.Api/`:

- `Controllers/` — thin ASP.NET controllers, one per feature area.
- `Commands/<Feature>/` — DispatchR requests + handlers (the business logic).
- `Domain/` — EF Core entities (`Account`, `Environment`, `Application`,
  `Identity`, `Metric`, …); `Domain/Auth/` holds the OpenIddict/Identity
  entities (`OpenIdUser`, `OpenIdApplication`, …).
- `Data/` — `ChurrosDbContext`, design-time factory, `Configuration/`
  (`IEntityTypeConfiguration` mappings).
- `Migrations/` — EF Core migrations + model snapshot.
- `Models/Dtos/` — request/response DTOs.
- `Mappers/` — Mapster mapping configs.
- `Services/` — domain services (`QuotaService`, `RunnerService`,
  `TemplateService`, `ClientNotificationService`, `WebTenantResolver`,
  metrics); `Services/Redis/` (cache, streams), `Services/Security/`
  (`AesGcmEncryption`, `SshKeyGenerator`), `Services/Share/` (YARP config +
  telemetry consumers).
- `Jobs/` — Quartz `IJob` implementations.
- `Middlewares/` — `MultiTenantMiddleware`, auth handlers, `NotificationHub`,
  `ResponseExceptionFilter`, proxy trace middleware.
- `Utils/` — extensions, exceptions (`HttpException`, `NotFoundException`, …),
  `MigrationExtension` (seeding), `TimeScaleExtensions`, `QuartzExtensions`,
  localization, JSON settings.
- `Resources/` — `Templates/*.yaml` and `Templates/Extensions/*.yaml` and
  `helm-values.yaml` (embedded resources), `Import/*.xlsx`,
  `Resources/Locales/` for `IStringLocalizer` resources.

## Key files / entry points

- `src/ChurrOS.Api/Program.cs` — all DI and pipeline configuration; also runs
  migrations and seeding on startup.
- `src/ChurrOS.AppHost/AppHost.cs` — Aspire orchestration + `ingress` routes.
- `src/ChurrOS.Api/Data/ChurrosDbContext.cs` — DbContext, tenant filters.
- `src/ChurrOS.Api/Utils/MigrationExtension.cs` — account/app/tunnel seeding.
- `src/ChurrOS.Api/Dockerfile` — multi-stage production image.

## Configuration

`appsettings.json` keys (override per environment / via user secrets):

- `ConnectionStrings:Database` — PostgreSQL; `ConnectionStrings:Cache` — Redis.
  Both are **empty in the committed file**; the AppHost supplies them via
  `AddConnectionString` (`Cache`, `Database`), so set them in user secrets /
  Aspire config for local runs.
- `BaseUrl` — OpenIddict issuer + OIDC authority.
- `ClientSecret` — secret for the `api` OIDC client.
- `MasterKey` — base64 AES-GCM key used to decrypt account/environment
  encryption keys (rotate per environment; do not ship the default).
- `Cors:Origins` — comma-separated allowed origins.
- `Owners`, `CreateAccount` — first-run seeding: if `CreateAccount=true` and no
  account exists, `MigrationExtension.CreateAccountAsync` bootstraps an account
  with the listed owner emails (AppHost defaults `CreateAccount` to `true`).
- `Quota:Environments` / `Quota:Applications` / `Quota:Network` — default
  per-tenant quotas (overridable per account via account metadata).
- `ExternalProviders:Microsoft:*` — Microsoft external login (disabled when
  `ClientId` is blank).
- `Tunnel:Host:Public` / `Tunnel:Host:Internal` / `Tunnel:PgsqlPassword` —
  tunnel service used to reach per-tenant environments;
  `MigrationExtension.InitilizeTunnelUser` seeds the tunnel user at startup.

User secrets IDs: API `b1ddb545-1c80-4f26-aaed-54b21f623156`, AppHost
`251087dc-e9da-4c61-badc-b5d419a9b0c3`.

Production-only env vars (read in `Program.cs`): `OPENIDDICT_SIGNING_PASSWORD`
and `OPENIDDICT_ENCRYPTION_PASSWORD` for the PFX certs at `/app/certs/`.

## Database

- **PostgreSQL** with the **TimescaleDB** extension. `deps/docker-compose.yml`
  starts `postgres:18-alpine` on `5432` for local development.
- On startup `Program.cs` runs `context.Database.Migrate()`, then
  `ApplyHypertablesAsync()` (converts entities annotated with
  `HypertableColumnAttribute` into Timescale hypertables) and
  `ApplyQuartzTables()` (creates the Quartz schema). **Migrations apply
  automatically on boot** — you usually only run `dotnet ef migrations add`.
- The model snapshot lives at `Migrations/ChurrosDbContextModelSnapshot.cs`.
  EF's `PendingModelChangesWarning` is intentionally ignored.

## Deployment

`src/ChurrOS.Api/Dockerfile` is a multi-stage build: `dotnet/sdk:10.0`
restores/builds/publishes (`-p:UseAppHost=false`), then `dotnet/aspnet:10.0`
runs `dotnet ChurrOS.Api.dll` (exposes `8080`/`8081`, non-root). `build.sh`
builds a local test image; versioned `linux/amd64` images are built and pushed
to `quay.io/churrostack/churros-api` by CI on release (see
[`docs/release-process.md`](../../docs/release-process.md)).
In production OpenIddict requires `/app/certs/signing.pfx` and
`/app/certs/encryption.pfx` — startup throws if they are missing.

## Conventions & gotchas

- **Mediator is DispatchR, not MediatR.** Requests use the two-type-parameter
  `IRequest<TRequest, TResponse>` form and handlers return `ValueTask<T>`.
  Always add commands as the paired `Xxx.cs` + `Xxx.Handler.cs`.
- **Every tenant-scoped entity is auto-filtered** by `AccountId` via global
  query filters. Background jobs and the YARP pipeline run outside an HTTP
  request, so they must call `ITenantResolver.SetAccountId(...)` /
  `SetIdentity(...)` before touching the DbContext (see `ScrapeMetricsJob`).
- `ChurrosDbContext` is registered with `ServiceLifetime.Transient`.
- The app forces `Request.Scheme = "https"` after forwarded headers; HTTPS
  redirection is only enabled in non-`DEBUG` builds.
- Npgsql legacy timestamp behavior is enabled and infinity conversions
  disabled — keep `DateTimeOffset`/timestamp handling consistent with that.
- In `DEBUG`, OpenIddict uses development certificates and EF enables sensitive
  data logging + detailed errors; production paths differ — check `#if DEBUG`.
- Localization supports `en`, `es`, `fr`, `it`, `pt`; use `IStringLocalizer` /
  `LocalizationService` for user-facing strings, not literals.
- The committed `MasterKey`, `ClientSecret`, and `PgsqlPassword` are
  placeholders — never rely on or reuse them outside local dev.
