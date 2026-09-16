# AzureFunctionsServerless - Entra ID, Managed Identity, and Bicep on Azure Functions

## Overview

This project builds a small, secured serverless API on Azure Functions'
**isolated worker** model: an HTTP-triggered notes service with no
database, protected by a hand-rolled Entra ID (Azure AD) bearer-token
authentication middleware, reading one secret from Key Vault through the
Function App's own system-assigned managed identity -- no connection
string, no access key, no client secret anywhere in configuration. Part 4
then provisions all of it with Bicep, including the two Entra ID App
Registrations a real client-credentials flow needs.

```
   client (client-credentials flow: client_id + client_secret)
       │
       │  POST /oauth2/v2.0/token  ->  Bearer access token
       ▼
┌─────────────────────────────────────────────────────────────┐
│  Azure Function App (isolated worker, Consumption plan)      │
│                                                                │
│  EntraIdAuthenticationMiddleware (IFunctionsWorkerMiddleware) │
│    -> validates: signature, issuer, audience, lifetime,       │
│       required app role (via EntraIdTokenValidator)           │
│                                                                │
│  GET  /api/health                 -- anonymous                │
│  GET  /api/notes                  -- secured, in-memory store │
│  POST /api/notes                  -- secured, in-memory store │
│  GET  /api/me                     -- secured, echoes claims   │
│  GET  /api/secure/config-check    -- secured, reads Key Vault │
│                                        via managed identity    │
└─────────────────────────────────────────────────────────────┘
       │                                    │
       ▼                                    ▼
  AzureWebJobsStorage                  Key Vault
  (host runtime plumbing,              (RBAC: "Key Vault Secrets User"
   not application data)                role granted to the Function's
                                         managed identity)
```

Two Entra ID App Registrations back the client-credentials flow: an **API
app** that exposes an app role (`Notes.Access`, `allowedMemberTypes:
["Application"]`), and a **client app** whose service principal is
assigned to that role. There is no user anywhere in this flow -- it's
app-only, service-to-service authentication.

## Learning Objectives

**Part 1 -- Build the Function API:**
- Isolated-worker fundamentals: `HostBuilder`/`ConfigureFunctionsWorkerDefaults`,
  `[Function]`, `HttpRequestData`/`HttpResponseData`
- Why `AzureWebJobsStorage` is required even with no application database
  (host runtime plumbing, not business data)
- `AuthorizationLevel.Anonymous` (no Azure function key required) versus
  real authentication (an entirely separate, older mechanism)
- The tradeoffs of an in-memory store on a Consumption plan

**Part 2 -- Secure it with Entra ID:**
- Why `[Authorize]`/`app.UseAuthentication()` aren't a drop-in fit for the
  classic isolated-worker model, and what building the alternative by hand
  teaches you
- Building a real `IFunctionsWorkerMiddleware` that fetches OIDC
  config/JWKS, validates a JWT, and checks a required app role
- The `MapInboundClaims = false` gotcha -- discovered, not just told
- Two App Registrations and why client-credentials flow is the natural fit
- Getting a real token with `curl`/an `.http` file and decoding it

**Part 3 -- Managed Identity + Key Vault:**
- The hardcoded-secret anti-pattern versus `DefaultAzureCredential` +
  system-assigned identity + `Azure.Security.KeyVault.Secrets`
- Managed Identity (who is calling) versus RBAC (what they're allowed to
  do) as two genuinely separate mechanisms
- `ConfigCheckFunction` as live proof

**Part 4 -- Infrastructure as Code with Bicep:**
- `main.bicep` and its modules, and the ARM-versus-Graph boundary that
  forces a `deploymentScript` for App Registrations
- A real circular-dependency problem between the Function App and Key
  Vault modules, and how it was actually resolved
- Verifying with `bicep build`/`bicep lint`, no Azure login required
- The one manual Entra ID directory-role prerequisite this template
  cannot automate
- A full deploy/teardown walkthrough with cost notes

## Prerequisites

- .NET 9 SDK (already required by this repo).
- **Parts 1-3 need no Docker, no Azure subscription, and no Azure
  Functions Core Tools.** `dotnet build`/`dotnet test` are sufficient to
  complete and verify all of the C# work -- confirmed clean in the
  environment this module was authored in (0 errors, 10/10 tests). Azure
  Functions Core Tools + **Azurite** (a local storage emulator) are
  documented for running the API as a real local HTTP server, but they are
  optional, not required to finish Parts 1-3. Neither is installed in the
  sandbox this module was authored in.
- **Part 4 needs an Azure subscription**, the Azure CLI (`az`) -- see
  [Install the Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)
  -- and the standalone Bicep CLI -- see
  [Install Bicep](https://learn.microsoft.com/azure/azure-resource-manager/bicep/install).
  Neither is installed in the sandbox this module was authored in, so Part
  4 is written to be read and run on your own machine; every `.bicep` file
  here was verified with `bicep build`/`bicep lint` (no Azure login
  needed), but no resource has actually been deployed.

## Project Structure

```
12-AzureFunctionsServerless/
├── FunctionApi/                  # <- YOUR WORKSPACE. Builds; has Health only.
│   ├── FunctionApi.csproj
│   ├── Program.cs
│   ├── Functions/HealthFunction.cs
│   └── local.settings.json.example
│
├── solution/FunctionApi/         # <- REFERENCE IMPLEMENTATION. Fully built, verified.
│   ├── Program.cs
│   ├── Functions/                #   Health, Notes (Get/Create), Me, ConfigCheck
│   ├── Security/                 #   EntraIdAuthenticationMiddleware, EntraIdTokenValidator,
│   │                             #   EntraIdConfigurationProvider, FunctionContextExtensions
│   ├── Notes/                    #   INotesStore, InMemoryNotesStore, Note
│   └── KeyVault/                 #   ISecretReader, KeyVaultSecretReader, KeyVaultOptions
│
├── tests/                        # <- 10 real, passing unit tests. No Azure/network needed.
│   ├── EntraIdTokenValidatorTests.cs   # valid/missing-role/wrong-audience/wrong-issuer/
│   │                                    # expired/wrong-key/malformed, all exercised for real
│   ├── TestTokenFactory.cs             # throwaway RSA key + self-signed test JWTs
│   └── InMemoryNotesStoreTests.cs
│
├── infra/                        # <- Bicep, verified with the standalone Bicep CLI.
│   ├── main.bicep                #   orchestrates every module below
│   ├── main.bicepparam
│   └── modules/
│       ├── storage.bicep         #   AzureWebJobsStorage's account
│       ├── appinsights.bicep     #   Log Analytics + Application Insights
│       ├── functionapp.bicep     #   Consumption (Y1) plan, system-assigned identity
│       ├── keyvault.bicep        #   RBAC role assignment, not access policies
│       └── appRegistrations.bicep #  deploymentScript: two App Registrations + role assignment
│
├── EXERCISE.md                   # Four parts, step by step
├── GETTING_STARTED.md            # Quick start + checkpointed Azure walkthrough
└── README.md                     # This file
```

## Running This Project

| Task | Command |
|---|---|
| Build the workspace project | `dotnet build 12-AzureFunctionsServerless/FunctionApi/FunctionApi.csproj` |
| Build the reference solution | `dotnet build 12-AzureFunctionsServerless/solution/FunctionApi/FunctionApi.Solution.csproj` |
| Run the unit tests (no Azure needed) | `dotnet test 12-AzureFunctionsServerless/tests/FunctionApi.Tests.csproj` |
| Run the API locally (needs Core Tools + Azurite) | `func start` from `FunctionApi/` or `solution/FunctionApi/` |
| Get a client-credentials token | `curl -X POST https://login.microsoftonline.com/<tenant>/oauth2/v2.0/token ...` (Part 2.8) |
| Validate Bicep (no Azure login needed) | `bicep build infra/main.bicep` / `bicep lint infra/main.bicep` |
| Preview a deployment | `az deployment group create --what-if ...` |
| Deploy to Azure (Part 4, your subscription) | `az group create ...` then `az deployment group create ...` |
| Tear everything down | `az group delete --name <rg-name> --yes` |

## Key Takeaways

- **`AuthorizationLevel.Anonymous` is not "no authentication."** It only
  controls Azure's function-key mechanism, a separate and older gate. Every
  function in this project is `Anonymous` at that layer and relies
  entirely on the custom Entra ID middleware plus an explicit
  `context.GetUser()` check for real security.
- **`AzureWebJobsStorage` is required even with zero application data.**
  It's the Functions host's own runtime plumbing (lease coordination,
  trigger bookkeeping) -- a completely different concern from the
  in-memory notes store this module deliberately uses instead of a real
  database.
- **`MapInboundClaims = false` is not optional decoration.**
  `JwtSecurityTokenHandler` silently remaps `"roles"` to a legacy
  WS-Federation claim URI by default. Without this flag, a token that
  plainly has the required role fails the role check anyway -- see
  `EXERCISE.md` Part 2.6 for the exact failure and fix, and
  `tests/EntraIdTokenValidatorTests.cs` for the regression test that
  catches it.
- **Managed Identity and RBAC are two different mechanisms.** Managed
  Identity answers "who is this caller"; an RBAC role assignment (here,
  "Key Vault Secrets User") answers "what is that caller allowed to do."
  Having one without the other gets you a `403`, not access.
- **App Registrations live in Microsoft Graph, not ARM.** There is no
  Bicep resource type for an Entra ID application, which is exactly why
  `infra/modules/appRegistrations.bicep` needs a `deploymentScript` to run
  `az ad` commands as part of an otherwise fully declarative deployment.
- **The Function-App/Key-Vault circular dependency was resolved by
  computing, not passing, the Key Vault URI.** A vault's URI is fully
  determined by its name, so `main.bicep` computes `keyVaultUri` from
  `keyVaultName` directly instead of reading it from the `keyVault`
  module's output -- breaking a cycle Bicep would otherwise reject
  outright.

## Common Mistakes

- **Assuming `AuthorizationLevel.Anonymous` means the endpoint is open.**
  It only bypasses the function-key check. Forgetting the
  `context.GetUser()` check inside the function body is what would
  actually leave an endpoint unsecured -- the trigger attribute alone
  proves nothing about authentication.
- **Deleting or "simplifying away" `MapInboundClaims = false`.** It looks
  redundant until a token with a real, present role starts failing the
  role check for no apparent reason.
- **Assuming a managed identity alone grants access to anything.** It
  grants an identity. Every resource that identity touches (Key Vault,
  Storage, anything else) still needs its own explicit RBAC role
  assignment.
- **Trying to write an App Registration as a plain Bicep resource.** There
  is no ARM resource type for it -- it has to go through Graph, via a
  `deploymentScript` or a separate tool (`az`, Microsoft Graph PowerShell,
  Terraform's `azuread` provider), never a native `resource` block.
- **Forgetting the Entra ID directory-role prerequisite before deploying.**
  `appRegistrations.bicep`'s `deploymentScript` needs its managed identity
  to already hold the "Application Developer" directory role (or higher)
  -- this cannot be granted by Bicep itself. See `GETTING_STARTED.md` Part
  4 for the exact command.
- **Picking a `nameSuffix` that collides globally.** The Storage Account
  and Key Vault both need globally unique names across all of Azure, not
  just your subscription -- a short, generic suffix like `test` will
  likely already be taken.
- **Swapping the in-memory notes store for something "more real" to test
  this module.** The store is deliberately simple so the lesson stays on
  the auth/identity pipeline; a real database is a different lesson
  (see `10-EntityFrameworkCore`, `09-EnterpriseCRUD`).

## Next Steps

After completing this module, review it alongside:
- **`06-CloudNative/Aspire`** -- another module with a real, cost-aware
  Azure deployment walkthrough written the same way (documented steps,
  explicit cost callouts, nothing run against a live subscription while
  authoring it).
- **`10-EntityFrameworkCore`** / **`09-EnterpriseCRUD`** -- for what a real
  persistence layer looks like, in contrast with this module's
  deliberately simple in-memory store.

Continue to **`13-GraphDatabaseNeo4j`** for the repo's third data model
(graph, after relational and document).

---

**Ready to start?** Open [GETTING_STARTED.md](GETTING_STARTED.md), then
work through [EXERCISE.md](EXERCISE.md) Part 1.
