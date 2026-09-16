# Aspire - .NET Aspire Orchestration and Azure Deployment

## Overview

Every other project in `06-CloudNative` teaches one cloud-native concern in
isolation (health checks, configuration, service composition) without an
orchestrator. This project puts several of those concerns behind a single
tool -- .NET Aspire -- and follows the same small app all the way from
`dotnet run` on your laptop to a real deployment on Azure Container Apps.

The app is a two-service storefront:

```
                    HTTP (service discovery)
 Storefront  ─────────────────────────────►  CatalogApi
   :5302                                        :5301
                                                  │   │
                                        Npgsql    │   │  Redis output cache
                                     (Aspire client)   (Aspire client
                                                  │      integration)
                                                  ▼   ▼
                                             Postgres  Redis
                                            (catalogdb) (cache)
```

`AppHost` is the one project that knows this whole picture exists. Every
other project only knows its own immediate neighbors, resolved by name
(`http://catalogapi`, `catalogdb`, `cache`) instead of a hardcoded address
-- Aspire's AppHost is what makes those names resolve to something real at
run time, in every environment, local or deployed.

## Learning Objectives

**Part A -- Aspire locally:**
- Orchestrate multiple projects and containers from one AppHost using
  `DistributedApplication.CreateBuilder`
- Understand what a shared `ServiceDefaults` project buys every service
  that references it (telemetry, health checks, service discovery, HTTP
  resilience) and why it exists as its own project
- Use Aspire's client integrations (`AddNpgsqlDbContext`, `AddRedisOutputCache`)
  and contrast them with hand-rolled `UseNpgsql`/manual health checks
- Use `WithReference` for service discovery between projects, and read a
  real `manifest.json` to see exactly what gets injected
- Read the Aspire dashboard: structured logs, one distributed trace across
  three resources, metrics, and a resource graph
- Manage resource lifecycle: parameter resources, `WithEnvironment`, and
  `WithReplicas`
- Write integration tests against a real running `DistributedApplication`
  with `Aspire.Hosting.Testing`

**Part B -- deploying to Azure with `azd`:**
- Understand what `azd` does with an Aspire AppHost: manifest -> Bicep ->
  provision -> deploy
- Read and reason about a real Aspire manifest before ever touching Azure
- Run `azd init`, `azd provision --preview`, `azd up`, and `azd down --purge`
  safely, with cost awareness at every spending step
- Know what `azd pipeline config` automates for CI/CD, without needing to
  wire up a pipeline for this exercise

## Prerequisites

- **Part A:** .NET 9 SDK (already required by this repo) and Docker
  Desktop (or another Docker-compatible engine) **running**. Aspire starts
  real Postgres and Redis containers; there is no in-memory fallback here.
- **Part B:** an Azure subscription, plus the Azure Developer CLI (`azd`)
  installed. See
  [Install `azd`](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
  -- not installed in the environment this exercise was authored in, so
  Part B is written to be read and followed on your own machine.

## Project Structure

```
Aspire/
├── AppHost/                 # <- YOUR WORKSPACE. Orchestration you build in Part A.
├── ServiceDefaults/         # <- YOUR WORKSPACE. Shared telemetry/health/discovery.
├── CatalogApi/              # <- YOUR WORKSPACE. Products API over Postgres + Redis.
├── Storefront/              # <- YOUR WORKSPACE. Calls CatalogApi via service discovery.
│
├── solution/                # <- REFERENCE IMPLEMENTATION. Same four projects, finished.
│   ├── AppHost/              #   includes manifest.json, a real generated artifact
│   ├── ServiceDefaults/
│   ├── CatalogApi/
│   └── Storefront/
│
├── tests/                   # <- Integration tests against the real AppHost (solution/)
│   ├── SmokeTests.cs
│   └── CatalogApiTests.cs
│
├── EXERCISE.md               # Part A (7 steps) + Part B (7 steps)
├── GETTING_STARTED.md        # Quick start + checkpointed Azure walkthrough
└── README.md                 # This file
```

## Running This Project

| Task | Command |
|---|---|
| Run the app locally (Part A) | `dotnet run --project AppHost` (workspace) or `dotnet run --project solution/AppHost` (reference) |
| Open the dashboard | URL printed to the console when the AppHost starts (also opens automatically) |
| Run the tests | `dotnet test tests/Aspire.CloudNative.Tests.csproj` (requires Docker; not runnable without it) |
| Regenerate the deployment manifest | `dotnet run --project solution/AppHost -- --publisher manifest --output-path manifest.json` (no Docker needed) |
| Initialize `azd` (Part B) | `azd init` from `solution/` |
| Preview Azure infrastructure before spending anything | `azd provision --preview` |
| Provision + deploy to Azure (Part B) | `azd up` |
| Tear everything down | `azd down --purge` |

## Key Takeaways

- **An Aspire AppHost is not a container orchestrator** -- it's a .NET
  program that starts and supervises other processes and containers, wires
  their configuration together, and hosts a dashboard. It has no equivalent
  of Kubernetes's reconciliation loop; it's closer to a very well-informed
  process supervisor.
- **`Projects.CatalogApi` is a source-generated type**, not a string path.
  It exists because `AppHost.csproj` marked the `ProjectReference` to
  CatalogApi with `IsAspireProjectResource="true"`.
- **`WithReference` is the mechanism, not a formality.** It's the one call
  that actually injects a connection string or a service discovery entry
  into a dependent project's environment. Nothing works without it, and
  nothing tells you it's missing except a runtime failure to resolve a name.
- **Aspire's client integrations do more than a plain `AddDbContext` call.**
  `AddNpgsqlDbContext<T>("catalogdb")` registers the `DbContext`, a health
  check, and OpenTelemetry instrumentation together. Compare this project's
  `CatalogApi/Program.cs` against `10-EntityFrameworkCore/EfCoreModeling`'s
  hand-rolled `UseNpgsql(connectionString)` to see the difference directly.
- **`launchSettings.json` is not optional decoration** for an Aspire
  project resource -- without a launch profile defining an HTTP endpoint,
  `WithReference` has nothing to inject and service discovery silently has
  no address to resolve to.
- **The manifest is a real, inspectable contract.** `solution/AppHost/manifest.json`
  in this repository was generated for real, and it is what `azd`/`aspire publish`
  actually reads -- there is no hidden step between your AppHost code and
  what gets deployed.
- **Azure deployment reuses the exact same code.** The only thing that
  changes between local and deployed is what `OTEL_EXPORTER_OTLP_ENDPOINT`
  points at and where the container images/connection strings physically
  live -- `ServiceDefaults`, the client integrations, and the service
  discovery calls are unchanged.

## Common Mistakes

- **Forgetting `launchSettings.json` on a project resource.** No launch
  profile with an `applicationUrl` means no HTTP binding in the manifest,
  which means `WithReference` has nothing to inject, which means service
  discovery fails at request time with no build-time warning.
- **Forgetting `WithReference` entirely.** The dependent project still
  compiles and still starts; it only fails once it actually tries to
  resolve the referenced resource's name or connection string.
- **Running the AppHost without Docker running.** `AddPostgres`/`AddRedis`
  need a real container engine; without one, the AppHost fails trying to
  start those resources while the project resources may appear to hang
  waiting on `WaitFor`.
- **Running `azd up` without `azd provision --preview` first.** `azd up`
  will happily provision and deploy in one shot; skipping the preview means
  you find out what got created (and what it costs) after the fact instead
  of before.
- **Swapping Postgres/Redis containers for managed Azure services as the
  default deployment path.** `AddAzurePostgresFlexibleServer()`/`AddAzureRedis()`
  are real, billed, always-on resources -- treat them as an optional
  advanced step, not what you reach for by default in a learning exercise.

## Next Steps

After completing this project, review it alongside:
- **`06-CloudNative/HealthChecks`** -- the manual version of the
  liveness/readiness split `ServiceDefaults` wires automatically here.
- **`10-EntityFrameworkCore/EfCoreModeling`** -- the hand-rolled
  `UseNpgsql` this project's `AddNpgsqlDbContext` replaces.
- **`09-EnterpriseCRUD`** -- another AppHost in this repository, for a
  second worked example of the same orchestration pattern at a larger
  scale.

---

**Ready to start?** Open [GETTING_STARTED.md](GETTING_STARTED.md), then
work through [EXERCISE.md](EXERCISE.md) Part A.
