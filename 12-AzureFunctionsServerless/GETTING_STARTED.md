# Getting Started with AzureFunctionsServerless

## Parts 1-3 Quick Start (no Azure, no Docker)

### 1. Navigate to the workspace

```bash
cd 12-AzureFunctionsServerless
```

### 2. Set up local settings

```bash
cp FunctionApi/local.settings.json.example FunctionApi/local.settings.json
```

This file is gitignored (see the repo root `.gitignore`) and needs **no
secrets** -- every value in it (a storage connection string that just
points at the local emulator, and the worker runtime) is non-sensitive.

### 3. Verify everything builds

```bash
dotnet build FunctionApi/FunctionApi.csproj
dotnet build solution/FunctionApi/FunctionApi.Solution.csproj
```

Both succeed with 0 errors as your starting point -- the workspace project
is mostly a stub (just `Program.cs` and `HealthFunction`), the `solution/`
project is the finished reference.

### 4. Run the tests

```bash
dotnet test tests/FunctionApi.Tests.csproj
```

**This genuinely works with no Azure account, no network access, and no
Docker.** All 10 tests pass by design: `EntraIdTokenValidatorTests.cs`
signs its own throwaway JWTs with an in-process RSA key
(`TestTokenFactory.cs`) and validates them against the real
`EntraIdTokenValidator` -- the actual production validation logic, not a
rewritten copy -- with zero real Entra ID tenant involved. Confirmed in
the environment this module was authored in:

```
Passed!  - Failed: 0, Passed: 10, Skipped: 0, Total: 10
```

### 5. Start the exercise

Open [EXERCISE.md](EXERCISE.md) and begin with Part 1. Run
`dotnet build`/`dotnet test` after each step -- don't wait until the end
to find out something doesn't compile.

### 6. (Optional) Run the API as a real local HTTP server

This needs two tools **not installed in the sandbox this module was
authored in** -- documented here so you can set them up on your own
machine if you want to `curl` real endpoints instead of just passing
tests:

- **Azure Functions Core Tools** -- see
  [Install Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local).
  Confirm with `func --version`.
- **Azurite** (local Azure Storage emulator) -- `AzureWebJobsStorage`'s
  `UseDevelopmentStorage=true` value points at this. Install via
  `npm install -g azurite`, or as a VS Code extension. Start it with
  `azurite` in a separate terminal before `func start`.

```bash
# terminal 1
azurite

# terminal 2
cd FunctionApi   # or solution/FunctionApi once you're testing the reference
func start
```

```bash
curl http://localhost:7071/api/health
```

None of Parts 1-3's actual learning goals require this -- `dotnet build`/
`dotnet test` are enough to complete and verify the C# work. This is only
for seeing real HTTP requests hit a real local host.

---

## Part 4 Walkthrough (Azure, your own subscription, costs a small amount)

Every step below that can spend money is marked. Read the whole step
before running the command. Nothing in this walkthrough was executed
against a real subscription while writing this module -- confirm every
command against your own installed `az`/Bicep CLI versions
(`az version`, `bicep --version`) as you go.

### Checkpoint 0: Install prerequisites

- **Azure CLI** -- confirm with `az version`. If missing:
  [Install the Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli).
- **Bicep CLI** (standalone, not just the `az bicep` extension) -- confirm
  with `bicep --version`. If missing:
  [Install Bicep](https://learn.microsoft.com/azure/azure-resource-manager/bicep/install).
- Sign in: `az login`, then confirm the right subscription is active:
  ```bash
  az account show
  az account set --subscription "<subscription-name-or-id>"   # if needed
  ```

**No cost yet.**

### Checkpoint 1: Validate the Bicep locally (no Azure access needed)

```bash
cd 12-AzureFunctionsServerless
bicep build infra/main.bicep
bicep lint infra/main.bicep
bicep lint infra/modules/appRegistrations.bicep
bicep lint infra/modules/functionapp.bicep
bicep lint infra/modules/keyvault.bicep
bicep lint infra/modules/storage.bicep
bicep lint infra/modules/appinsights.bicep
```

Confirm zero errors and zero warnings on every file -- this is exactly
what was verified while authoring this module, with no Azure login
required for either command.

**No cost.** Pure local compilation/linting.

### Checkpoint 2: Grant the Entra ID directory-role prerequisite (one-time, manual)

**Do this before your first deployment, or it will fail partway through.**

`infra/modules/appRegistrations.bicep` runs `az ad app create` (and
related commands) inside a `Microsoft.Resources/deploymentScripts`
resource, using a **user-assigned managed identity** it creates
(`deploymentIdentity`). That identity needs the Entra ID **"Application
Developer"** directory role (or higher, e.g. "Application Administrator"
or "Cloud Application Administrator") before the script can create App
Registrations -- otherwise `az ad app create` inside the script fails with
an authorization error, and the whole deployment fails partway through
(after the Storage Account and App Insights already deployed, which is a
confusing place to fail).

This is a **Microsoft Graph directory-role assignment**, not an ARM RBAC
role -- there is no Bicep resource type for it, and granting it requires
**Global Administrator** or **Privileged Role Administrator** rights in
your tenant (a deliberately higher bar than "can deploy Azure
infrastructure"). If that isn't you, find whoever in your tenant holds
that role and ask them to run this.

Because this identity doesn't exist yet before the first deployment
attempt, there's a chicken-and-egg problem: you need to deploy far enough
to create `deploymentIdentity`, grant it the role, then deploy again (or
retry) for the app-registration step to succeed. Two ways to handle this:

**Option A -- deploy once, expect the app-registration step to fail, grant the role, redeploy (safe, idempotent):**

```bash
az group create --name <rg-name> --location <location>
az deployment group create \
  --resource-group <rg-name> \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam \
  --parameters keyVaultSecretValue='whatever you want for the demo secret'
```

Expect this first attempt to fail at the `appRegistrations` module. Then
find the identity it created:

```bash
az identity show \
  --resource-group <rg-name> \
  --name id-<nameSuffix>-appreg \
  --query principalId -o tsv
```

Grant it the directory role using Microsoft Graph PowerShell (the current
supported way to manage Entra ID directory-role assignments -- the older
`az ad` role-assignment commands here are for Azure RBAC, not Entra ID
directory roles):

```powershell
Connect-MgGraph -Scopes "RoleManagement.ReadWrite.Directory"

$roleId = (Get-MgDirectoryRole -Filter "displayName eq 'Application Developer'").Id
# If the role has never been activated in your tenant, activate it from its template first:
# $template = Get-MgDirectoryRoleTemplate -Filter "displayName eq 'Application Developer'"
# $role = New-MgDirectoryRole -DisplayName $template.DisplayName -Description $template.Description -RoleTemplateId $template.Id
# $roleId = $role.Id

New-MgDirectoryRoleAssignment -PrincipalId "<principalId-from-above>" -RoleDefinitionId $roleId -DirectoryScopeId "/"
```

(Or via the Portal: **Entra ID -> Roles and administrators -> Application
Developer -> Add assignments** -> search for the managed identity by name
`id-<nameSuffix>-appreg` -> add it.)

Then re-run the same `az deployment group create` command. Bicep
deployments are idempotent -- resources that already exist and match are
left alone, and the app-registration script's own idempotency (it checks
for an existing app by display name before creating one) makes it safe to
retry.

**Option B -- grant the role to your own signed-in user or a pre-created identity before deploying at all**, if you'd rather not deploy-fail-retry. The mechanism is the same `New-MgDirectoryRoleAssignment` call, just run against a principal you created ahead of time.

**No cost for the role grant itself** (it's a permission, not a resource).

### Checkpoint 3: Fill in your deployment parameters

Edit `infra/main.bicepparam`, or override at the command line:

```bicep
using 'main.bicep'

param nameSuffix = 'yourinitials123'   // must be short and globally unique-ish
param keyVaultSecretValue = 'placeholder-replace-at-deploy-time'
```

**Never commit a real secret value** into `main.bicepparam` -- pass it at
the command line instead, as shown in Checkpoint 4.

**No cost.** Still entirely local.

### Checkpoint 4: Preview before you deploy

```bash
az deployment group create \
  --resource-group <rg-name> \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam \
  --parameters keyVaultSecretValue='whatever you want' \
  --what-if
```

Read the full output. `--what-if` shows exactly what would be created,
modified, or deleted without actually doing it -- the same "stop and read
before spending anything" discipline used in other modules' Azure
deployment guides.

**No cost.** `--what-if` does not create resources.

### Checkpoint 5: Deploy for real

```bash
az group create --name <rg-name> --location <location>
az deployment group create \
  --resource-group <rg-name> \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam \
  --parameters keyVaultSecretValue='whatever you want'
```

**Starts costing money, but very little.** This is the cheapest module in
the repo to run:
- The Function App runs on a **Consumption (Y1) plan** -- true
  pay-per-execution, with a substantial monthly free grant of executions
  and GB-seconds. Testing this module's handful of endpoints a few dozen
  times will not exhaust the free grant.
- **Key Vault** with one secret and light read traffic costs fractions of
  a cent.
- **Application Insights / Log Analytics** ingestion is metered, but the
  volume this module generates in a short exercise is negligible.
- Contrast with `06-CloudNative/Aspire`'s Container Apps environment
  (provisioned compute, billed even mostly idle) or a managed
  database/Cosmos DB module's provisioned or RU-based billing -- there is
  no comparably "always-on" resource here.

When it finishes, capture the outputs:

```bash
az deployment group show \
  --resource-group <rg-name> \
  --name main \
  --query properties.outputs
```

You need `functionAppHostName`, `apiAppId`, `clientAppId`, `tenantId`, and
`clientSecret` (marked `@secure()` -- it won't print by default in some
`az` output modes; use `--query properties.outputs.clientSecret.value -o tsv`
to retrieve it explicitly) for the next checkpoint.

### Checkpoint 6: Get a token and call the deployed API

```bash
curl -X POST "https://login.microsoftonline.com/<tenantId>/oauth2/v2.0/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=<clientAppId>" \
  -d "client_secret=<clientSecret>" \
  -d "scope=api://<apiAppId>/.default" \
  -d "grant_type=client_credentials"
```

Take the `access_token` from the response:

```bash
curl https://<functionAppHostName>/api/health

curl https://<functionAppHostName>/api/me \
  -H "Authorization: Bearer <access_token>"

curl https://<functionAppHostName>/api/secure/config-check \
  -H "Authorization: Bearer <access_token>"
```

Confirm `/api/me` echoes `"roles": ["Notes.Access"]` among its claims, and
`/api/secure/config-check` returns a masked preview of the demo secret --
proof the Function read it via managed identity with no secret in its own
configuration.

**No additional cost beyond Checkpoint 5** -- these are just requests
against an already-deployed, consumption-billed Function.

### Checkpoint 7: Verify, then tear down

Once you've confirmed the deployed app works:

```bash
az group delete --name <rg-name> --yes --no-wait
```

Then verify it's actually gone, don't just trust the exit code:

```bash
az group show --name <rg-name>
```

This should fail with a "could not be found" error, not return JSON. If it
still returns something, deletion is still in progress (asynchronous) --
check again in a few minutes, or check the Azure Portal's Activity Log for
the resource group.

**Cost stops once the resource group is fully deleted**, not the moment
`az group delete` returns. Note the Key Vault has `enablePurgeProtection: true`
(see `infra/modules/keyvault.bicep`) -- it soft-deletes rather than
disappearing immediately, and won't be purgeable until its retention
period elapses. This does not continue to cost you anything while
soft-deleted; it's just not immediately gone if you go looking for it in
the Portal.

---

## Troubleshooting

**`az: command not found` / `bicep: command not found`.**
Neither is installed, or isn't on your `PATH`. Follow the install links in
Checkpoint 0, then open a new terminal -- installers frequently require a
fresh shell to pick up the updated `PATH`.

**The `appRegistrations` deployment step fails with an authorization
error mentioning Graph or "Insufficient privileges."**
This is almost always the Checkpoint 2 prerequisite -- the
`deploymentScript`'s managed identity doesn't yet have the "Application
Developer" directory role. Re-read Checkpoint 2 and confirm the role
assignment actually took effect (`Get-MgDirectoryRoleAssignment` filtered
to that principal ID) before retrying the deployment. Directory-role
assignments can also take a few minutes to propagate -- if you just
granted it, wait a couple of minutes and retry.

**`az deployment group create` fails with a name-collision error on the
Storage Account or Key Vault.**
Both need **globally unique** names across all of Azure (`st<nameSuffix>func`
and `kv-<nameSuffix>-func`), not just your subscription. Pick a longer,
more distinctive `nameSuffix` in `main.bicepparam` -- initials plus a few
random characters works well -- and redeploy.

**The deployment succeeds but calling any secured endpoint returns `401`.**
Check, in order: (1) did you request the token with
`scope=api://<apiAppId>/.default` exactly, not a different scope string;
(2) does the decoded token (paste it into [jwt.ms](https://jwt.ms)) show
`"roles": ["Notes.Access"]` -- if not, the app-role assignment from
`appRegistrations.bicep` didn't take, re-run the deployment; (3) is the
`Authorization` header formatted exactly as `Bearer <token>` with one
space.

**`GET /api/secure/config-check` returns a 500 or times out.**
Usually the RBAC role assignment hasn't propagated yet immediately after
deployment (can take a minute or two), or `KeyVault__VaultUri` doesn't
match the deployed vault's actual URI -- check the Function App's
Configuration blade in the Portal against the output of
`az keyvault show --name kv-<nameSuffix>-func --query properties.vaultUri`.

**Tests in `tests/` fail unexpectedly.**
They need no Azure access or Docker at all (see the Quick Start section
above) -- if they fail, it's a real regression in the validation logic or
test setup, not an environment problem. Run
`dotnet test tests/FunctionApi.Tests.csproj -v normal` for more detail on
which specific assertion failed.

---

**Ready?** Head to [EXERCISE.md](EXERCISE.md) and start with Part 1.
