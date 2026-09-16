// App Registrations live in Microsoft Graph, not the ARM control plane --
// there is no "Microsoft.AzureActiveDirectory/applications" resource type
// in classic Bicep. This module runs `az ad app create` (and friends)
// inside a deploymentScript so the whole deployment stays one
// `az deployment group create` command instead of a separate manual step.
//
// PREREQUISITE this module cannot automate: the user-assigned identity
// below needs the Entra ID "Application Developer" directory role (or
// higher) BEFORE this deploys, or `az ad app create` inside the script will
// fail with an authorization error. Directory role assignment is itself a
// Graph operation with no ARM resource type, and granting it requires
// Global Administrator / Privileged Role Administrator rights -- see
// GETTING_STARTED.md Part 4 for the one-time manual command. This is the
// honest boundary of "automate the infrastructure with Bicep": ARM-owned
// resources are fully declarative below; the one Entra ID directory-role
// grant is not, and pretending otherwise would just hide where it happens.
param location string
param uamiName string
param apiAppDisplayName string
param clientAppDisplayName string
param appRoleValue string
param utcValue string = utcNow()

resource deploymentIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: uamiName
  location: location
}

resource appRegistrationScript 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'create-app-registrations'
  location: location
  kind: 'AzureCLI'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${deploymentIdentity.id}': {}
    }
  }
  properties: {
    azCliVersion: '2.65.0'
    retentionInterval: 'P1D'
    cleanupPreference: 'OnSuccess'
    timeout: 'PT15M'
    // Re-runs the script on every deployment (it's idempotent) rather than
    // only the first time -- keeps the app role assignment self-healing if
    // someone removes it by hand in the Portal.
    forceUpdateTag: utcValue
    environmentVariables: [
      { name: 'API_APP_NAME', value: apiAppDisplayName }
      { name: 'CLIENT_APP_NAME', value: clientAppDisplayName }
      { name: 'APP_ROLE_VALUE', value: appRoleValue }
    ]
    // Idempotent: re-running the deployment looks up an existing app by
    // display name before creating one, so `az deployment group create`
    // stays safe to run more than once (e.g. after `az deployment group
    // create` partially fails and you fix a parameter and retry).
    scriptContent: '''
      set -euo pipefail

      api_app_id=$(az ad app list --display-name "$API_APP_NAME" --query "[0].appId" -o tsv)
      if [ -z "$api_app_id" ]; then
        api_app_id=$(az ad app create --display-name "$API_APP_NAME" --sign-in-audience AzureADMyOrg --query appId -o tsv)
      fi

      # The API's own Application ID URI -- this is the "audience" the
      # Function validates tokens against (EntraId__Audience).
      az ad app update --id "$api_app_id" --identifier-uris "api://$api_app_id"

      app_role_id=$(az ad app show --id "$api_app_id" --query "appRoles[?value=='$APP_ROLE_VALUE'] | [0].id" -o tsv)
      if [ -z "$app_role_id" ] || [ "$app_role_id" == "null" ]; then
        app_role_id=$(python3 -c "import uuid; print(uuid.uuid4())")
        existing_roles=$(az ad app show --id "$api_app_id" --query "appRoles" -o json)
        new_role=$(python3 -c "
import json, sys
roles = json.loads(sys.argv[1])
roles.append({
    'allowedMemberTypes': ['Application'],
    'description': 'Access to the Notes API (app-only, client credentials flow).',
    'displayName': sys.argv[2],
    'id': sys.argv[3],
    'isEnabled': True,
    'value': sys.argv[2]
})
print(json.dumps(roles))
" "$existing_roles" "$APP_ROLE_VALUE" "$app_role_id")
        echo "$new_role" > /tmp/approles.json
        az ad app update --id "$api_app_id" --app-roles @/tmp/approles.json
      fi

      api_sp_id=$(az ad sp list --filter "appId eq '$api_app_id'" --query "[0].id" -o tsv)
      if [ -z "$api_sp_id" ]; then
        api_sp_id=$(az ad sp create --id "$api_app_id" --query id -o tsv)
      fi

      client_app_id=$(az ad app list --display-name "$CLIENT_APP_NAME" --query "[0].appId" -o tsv)
      client_secret=""
      if [ -z "$client_app_id" ]; then
        client_app_id=$(az ad app create --display-name "$CLIENT_APP_NAME" --sign-in-audience AzureADMyOrg --query appId -o tsv)
        client_secret=$(az ad app credential reset --id "$client_app_id" --years 1 --query password -o tsv)
      fi

      client_sp_id=$(az ad sp list --filter "appId eq '$client_app_id'" --query "[0].id" -o tsv)
      if [ -z "$client_sp_id" ]; then
        client_sp_id=$(az ad sp create --id "$client_app_id" --query id -o tsv)
      fi

      # Assigns the client's service principal to the API's app role -- the
      # actual "this client may call this API" grant for a client-credentials
      # (app-only) flow. There is no `az ad` subcommand for this; it's a
      # direct Graph call. Safe to re-run: Graph returns 400 if the
      # assignment already exists, which this ignores.
      az rest --method POST \
        --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$api_sp_id/appRoleAssignedTo" \
        --body "{\"principalId\":\"$client_sp_id\",\"resourceId\":\"$api_sp_id\",\"appRoleId\":\"$app_role_id\"}" \
        || echo "App role assignment already exists, continuing."

      jq -n \
        --arg apiAppId "$api_app_id" \
        --arg clientAppId "$client_app_id" \
        --arg clientSecret "$client_secret" \
        --arg appRoleId "$app_role_id" \
        '{apiAppId: $apiAppId, clientAppId: $clientAppId, clientSecret: $clientSecret, appRoleId: $appRoleId}' > $AZ_SCRIPTS_OUTPUT_PATH
    '''
  }
}

output apiAppId string = appRegistrationScript.properties.outputs.apiAppId
output clientAppId string = appRegistrationScript.properties.outputs.clientAppId
output appRoleId string = appRegistrationScript.properties.outputs.appRoleId
@secure()
output clientSecret string = appRegistrationScript.properties.outputs.clientSecret
