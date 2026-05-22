# Fix CI build flake — NuGet first-run migration race

**Date:** 2026-05-22
**Area:** `.github/workflows/ci.yml` (`validate` job)

## Trigger

The `validate` job (`pnpm nx affected -t lint test build`) failed on a PR
touching `apps/api` with:

```
> nx run api:build
> dotnet build src/ChurrOS.slnx

System.IO.IOException: The system cannot open the device or file specified. :
'NuGet-Migrations'. One or more system calls failed:
mkdir("/tmp/.dotnet/shm/session2094", AllUsers_ReadWriteExecute) == -1;
errno == EEXIST;
   at System.Threading.Mutex.CreateMutexCore(...)
   at NuGet.Common.Migrations.MigrationRunner.Run(...)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Program.ConfigureDotNetForFirstTimeUse(...)
```

## Root cause

Not a code defect — a CI infrastructure race, unrelated to the PR's changes.

`nx affected` runs tasks in parallel (Nx default parallelism = 3). The PR
touches `apps/api`, whose project has both a `build` and a `test` target.
`build` only `dependsOn: ["^build"]` (dependency builds) and `test` has no
`dependsOn`, so `api:build` (`dotnet build`) and `api:test` (`dotnet test`)
start concurrently.

On a fresh GitHub-hosted runner both are the *first* `dotnet` invocations, so
both run the .NET SDK first-run experience → `DotnetFirstTimeUseConfigurer`
→ NuGet `MigrationRunner.Run()`. The runner creates the named mutex
`NuGet-Migrations`; on Linux .NET backs named mutexes with a shared-memory
directory under `/tmp/.dotnet/shm/session<id>/`. Both processes share the same
session id, both `mkdir()` the same `session2094` directory, the loser gets
`errno == EEXIST`, and .NET's named-mutex creation throws instead of
tolerating it. The build fails.

It surfaced now because this is the first PR exercising two parallel `dotnet`
tasks (an `apps/api` change makes both `api:build` and `api:test` affected).
It is timing-dependent, so it would also have been intermittently green.

`release.yml` is unaffected: it runs `dotnet` only inside per-image Docker
builds (one process per isolated job), never in parallel on the host.

## Fix

Add a step to the `validate` job, after `actions/setup-dotnet` and before
`pnpm nx affected`, that runs one `dotnet` command serially:

```yaml
- name: Warm up .NET SDK first-run experience
  run: dotnet nuget locals all --list
```

This completes the first-run migration once and writes the first-use
sentinel; the later parallel `dotnet build`/`dotnet test` tasks then skip the
migration entirely, so there is no mutex-directory race.

`dotnet --info` / `dotnet --version` are **not** suitable warm-up commands —
verified locally with a fresh `DOTNET_CLI_HOME`, they short-circuit and never
trigger the first-run configurer. `dotnet nuget locals all --list` does
(read-only, fast, no side effects).

## Verification

- Local check with a fresh `DOTNET_CLI_HOME`: `dotnet nuget locals all --list`
  writes `<sdk-version>.dotnetFirstUseSentinel`; `dotnet --info` and
  `dotnet --version` do not.
- CI: re-run the `validate` job — `api:build` and `api:test` complete without
  the `NuGet-Migrations` / `mkdir EEXIST` error.
