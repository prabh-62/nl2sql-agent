// The public demo: one Linux App Service on the Free (F1) plan that serves the API and the built
// React app. Deployed by GitHub Actions (deploy.yml) as nl2sql-deployer. It runs as the existing
// nl2sql-reader identity (SELECT on the ai views, OpenAI User on Foundry), so there are no secrets:
// every connection signs in with that managed identity.

@description('Region for the plan and app; next to the database.')
param location string = 'westus2'

@description('Web app name; the URL becomes https://<name>.azurewebsites.net.')
param appName string = 'ms-sql-nl2sql-agent'

@description('Existing SQL server (Entra-only) and its free-tier database.')
param sqlServerName string = 'panjab'
param sqlDatabaseName string = 'rasoolpur'

@description('Existing Foundry resource and the chat model deployment to use.')
param foundryName string = 'hemingford-9901-resource'
param chatDeployment string = 'gpt-5.4-mini'

resource reader 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: 'nl2sql-reader'
}

resource plan 'Microsoft.Web/serverfarms@2024-11-01' = {
  name: '${appName}-plan'
  location: location
  kind: 'linux'
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  properties: {
    reserved: true // Linux
  }
}

resource app 'Microsoft.Web/sites@2024-11-01' = {
  name: appName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${reader.id}': {}
    }
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    clientAffinityEnabled: false
    siteConfig: {
      linuxFxVersion: 'DOTNET|11'
      // The app is published self-contained (App Service has .NET 11 RC1, the app needs RC2), so
      // run its own executable instead of `dotnet Nl2Sql.Api.dll`. Zip deploys don't keep the
      // executable bit, hence the chmod.
      appCommandLine: 'chmod +x /home/site/wwwroot/Nl2Sql.Api && /home/site/wwwroot/Nl2Sql.Api'
      alwaysOn: false // not available on F1: the app sleeps when idle and cold-starts on the next request
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      http20Enabled: true
      appSettings: [
        // DefaultAzureCredential: use only the nl2sql-reader managed identity.
        { name: 'AZURE_CLIENT_ID', value: reader.properties.clientId }
        { name: 'AZURE_TOKEN_CREDENTIALS', value: 'ManagedIdentityCredential' }
        // App Service forwards to port 8080; the app's own executable doesn't get Oryx's defaults.
        { name: 'ASPNETCORE_HTTP_PORTS', value: '8080' }
        { name: 'WEBSITES_PORT', value: '8080' }
        {
          name: 'ConnectionStrings__db'
          value: 'Server=tcp:${sqlServerName}${environment().suffixes.sqlServerHostname},1433;Database=${sqlDatabaseName};Authentication=Active Directory Managed Identity;User Id=${reader.properties.clientId};Encrypt=True;Connect Timeout=90'
        }
        {
          name: 'ConnectionStrings__foundry'
          value: 'Endpoint=https://${foundryName}.cognitiveservices.azure.com/;Deployment=${chatDeployment}'
        }
      ]
    }
  }
}

// No publishing passwords: deployments authenticate with Entra (the deployer's OIDC token).
resource ftpCredentials 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-11-01' = {
  parent: app
  name: 'ftp'
  properties: {
    allow: false
  }
}

resource scmCredentials 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-11-01' = {
  parent: app
  name: 'scm'
  properties: {
    allow: false
  }
}

output url string = 'https://${app.properties.defaultHostName}'
output name string = app.name
