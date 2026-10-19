// The nl2sql-reader managed identity: the only identity that runs model-generated SQL in Azure.
// App Service uses it in production (M6); GitHub Actions signs in as it with OIDC to run the
// safety tests. Its database user only has SELECT on schema ai (added by the migration service).
// It gets no Azure role assignments here.

@description('Location for the identity.')
param location string = resourceGroup().location

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

output name string = reader.name
output clientId string = reader.properties.clientId
output principalId string = reader.properties.principalId
output tenantId string = reader.properties.tenantId
