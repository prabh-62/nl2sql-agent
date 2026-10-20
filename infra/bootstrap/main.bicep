// One-time bootstrap, applied by an Owner from their machine (deploy.sh). Everything after this is
// deployed by GitHub Actions with OIDC.
//
// nl2sql-reader: the only identity that runs model-generated SQL in Azure. App Service runs as it;
// GitHub Actions signs in as it for the safety tests and evals. Its database user only has SELECT
// on schema ai (added by the migration service). Its only Azure role: calling the Foundry models.
//
// nl2sql-deployer: what GitHub Actions deploys the app with (infra/app and the code). It can manage
// web apps and App Service plans in this resource group and attach nl2sql-reader to them, and
// nothing else: no access to the SQL server, the database, Foundry or any data.

@description('Location for the identities.')
param location string = resourceGroup().location

@description('Existing Foundry (Azure OpenAI) resource in this resource group.')
param foundryName string = 'hemingford-9901-resource'

@description('GitHub repository allowed to sign in as this identity, as it appears in the OIDC token subject. GitHub includes immutable IDs: owner@ownerId/name@repositoryId.')
param githubSubjectRepository string

resource reader 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'nl2sql-reader'
  location: location
}

// Federated credentials on one identity must be created one at a time, hence the dependsOn.
resource githubMain 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: reader
  name: 'github-main'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${githubSubjectRepository}:ref:refs/heads/main'
    audiences: ['api://AzureADTokenExchange']
  }
}

resource githubPullRequests 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: reader
  name: 'github-pull-request'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${githubSubjectRepository}:pull_request'
    audiences: ['api://AzureADTokenExchange']
  }
  dependsOn: [githubMain]
}

resource foundry 'Microsoft.CognitiveServices/accounts@2025-09-01' existing = {
  name: foundryName
}

// Cognitive Services OpenAI User: call model deployments; can't manage the resource or read keys.
var openAiUserRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')

resource foundryAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(foundry.id, reader.id, openAiUserRole)
  scope: foundry
  properties: {
    principalId: reader.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: openAiUserRole
  }
}

resource deployer 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'nl2sql-deployer'
  location: location
}

// Only deploys from main can sign in as the deployer.
resource deployerGithubMain 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: deployer
  name: 'github-main'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${githubSubjectRepository}:ref:refs/heads/main'
    audiences: ['api://AzureADTokenExchange']
  }
}

var websiteContributorRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'de139f84-1756-47ae-9be6-808fbbe84772')
var webPlanContributorRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '2cc479cb-7b4d-49a8-b449-8c00fd0f0a4b')
var managedIdentityOperatorRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'f1a07417-d97a-45cb-824c-7a7467783830')

// Web apps (and deployments of them) in this resource group.
resource deployerWebsites 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, deployer.id, websiteContributorRole)
  properties: {
    principalId: deployer.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: websiteContributorRole
  }
}

// App Service plans in this resource group.
resource deployerPlans 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, deployer.id, webPlanContributorRole)
  properties: {
    principalId: deployer.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: webPlanContributorRole
  }
}

// Attach nl2sql-reader to the web app; scoped to that one identity.
resource deployerAssignsReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(reader.id, deployer.id, managedIdentityOperatorRole)
  scope: reader
  properties: {
    principalId: deployer.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: managedIdentityOperatorRole
  }
}

output deployerClientId string = deployer.properties.clientId
output name string = reader.name
output clientId string = reader.properties.clientId
output principalId string = reader.properties.principalId
output tenantId string = reader.properties.tenantId
