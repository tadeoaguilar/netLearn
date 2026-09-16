# Getting Started with Aspire

## Part A Quick Start (local, Docker required)

### 1. Confirm Docker is running

```bash
docker ps
```

If this errors instead of printing an (possibly empty) table, start Docker
Desktop first. Aspire starts real Postgres and Redis containers -- there is
no in-memory fallback in this project.

### 2. Navigate to the workspace

```bash
cd 06-CloudNative/Aspire
```

### 3. Verify everything builds before you change anything

```bash
dotnet build AppHost/AppHost.csproj
dotnet build CatalogApi/CatalogApi.csproj
dotnet build Storefront/Storefront.csproj
dotnet build ServiceDefaults/ServiceDefaults.csproj
```

All four should succeed. This is your starting point -- mostly stubs, but
nothing broken.

### 4. Run the AppHost

```bash
dotnet run --project AppHost
```

Watch the console output. Within a few seconds you should see a line like:

```
Login to the dashboard at https://localhost:17XXX/login?t=...
```

Open that URL. This is the Aspire dashboard -- it will keep running for as
long as the AppHost process does (`Ctrl+C` in the terminal stops
everything: the AppHost and everything it started).

### 5. Start the exercise

Open [EXERCISE.md](EXERCISE.md) and begin with Part A, Step 1. Re-run
`dotnet run --project AppHost` after each step and check the dashboard --
don't wait until the end to see if it works.

---

## Part B Walkthrough (Azure, your own subscription, costs real money)

Every step below that can spend money is marked. Read the whole step
before running the command.

### Checkpoint 0: Install prerequisites

- Azure CLI (`az`) -- confirm with `az version`.
- Azure Developer CLI (`azd`) -- confirm with `azd version`. If missing,
  see [Install `azd`](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd).
- Sign in: `az login`, then `azd auth login`. Both are needed -- they
  authenticate different tools.

**No cost yet.** Nothing has touched Azure resources.

### Checkpoint 1: Regenerate and read the manifest (no Azure access needed)

```bash
cd 06-CloudNative/Aspire
dotnet run --project solution/AppHost -- --publisher manifest --output-path manifest.json
```

Open the resulting `manifest.json`. Confirm you can find: the container
images for `postgres` and `cache`, the `.csproj` paths for `catalogapi` and
`storefront`, and the `services__catalogapi__http__0` environment variable
under `storefront`. This is EXERCISE.md Part B.2 -- do it now if you
haven't.

**No cost.** This command stops before contacting Azure at all.

### Checkpoint 2: `azd init`

```bash
cd solution
azd init
```

Follow the prompts (environment name, subscription, region). This writes
local files (`azure.yaml`, `.azure/<env>/`) -- it does not provision
anything yet.

**No cost.** Still entirely local.

### Checkpoint 3: Preview before you provision

```bash
azd provision --preview
```

Read the full output. This is the list of everything `azd up` is about to
create. If anything looks unexpected (a resource type you don't recognize,
a region you didn't intend), stop here and investigate before continuing.

**No cost.** `--preview` does not create resources.

### Checkpoint 4: Provision and deploy

```bash
azd up
```

This is the step that starts costing money -- Container Apps environment,
Container Registry, Log Analytics workspace, and the Container Apps
themselves are all billed resources, even though this exercise keeps them
on consumption-based pricing by keeping Postgres/Redis as containers rather
than managed services (see `README.md`'s cost warning). Expect this to take
several minutes. When it finishes, `azd` prints the deployed endpoints --
open Storefront's URL and confirm you get the same welcome-message response
you saw locally in Part A.

**Costs money while the resources exist.** Consumption-priced, but not
free, and it keeps billing until you tear it down.

### Checkpoint 5: Verify, then tear down

Once you've confirmed the deployed app works:

```bash
azd down --purge
```

Then verify the resource group is actually gone:

```bash
az group show --name <your-resource-group-name>
```

This should fail with a "could not be found" error. If it still returns
JSON, the deletion is likely still in progress (Azure deletions are
asynchronous) -- wait a few minutes and check again, or check the Azure
Portal's Activity Log for the resource group.

**Cost stops once the resource group is fully deleted**, not the moment
`azd down` returns.

---

## Troubleshooting

**The Aspire dashboard doesn't open / the URL 404s.**
Check the AppHost's console output for the actual URL and token -- it
changes per run. If the browser didn't auto-launch, copy the full URL
(including the `?t=` token) manually. If the AppHost process crashed before
printing a URL, the problem is upstream of the dashboard -- check the
console for a stack trace first.

**Postgres or Redis containers won't start.**
Confirm Docker is actually running (`docker ps`). If Docker is running but
the containers still fail, check the dashboard's console log for that
resource -- a common cause is a port already in use by another local
Postgres/Redis instance, or a stale container/volume left over from a
previous run (`docker ps -a` to check, `docker rm` to clean up).

**`azd: command not found`.**
`azd` isn't installed or isn't on your `PATH`. Follow
[Install `azd`](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd)
for your OS, then open a new terminal (installers frequently require a
fresh shell to pick up the updated `PATH`).

**`azd up` fails asking you to log in, or with an authorization error.**
Run `az login` and `azd auth login` (both, not just one -- they maintain
separate credentials) and confirm `az account show` reports the
subscription you expect before retrying.

**Tests in `tests/` fail immediately, before doing anything useful.**
They need Docker running, same as running the AppHost directly does --
`Aspire.Hosting.Testing` starts the real `DistributedApplication`. If
Docker isn't available (as in a CI sandbox with no container runtime), the
tests will fail to start any resource; this is expected there and is why
this exercise only requires them to **build**, not run, in such an
environment.

---

**Ready?** Head to [EXERCISE.md](EXERCISE.md) and start with Part A, Step 1.
