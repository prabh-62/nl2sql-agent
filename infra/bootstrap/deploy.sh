#!/usr/bin/env bash
# One-time bootstrap (needs Owner on the resource group): creates the nl2sql-reader and
# nl2sql-deployer managed identities with their roles and GitHub OIDC credentials, and stores their
# non-secret IDs as GitHub repository variables. Run signed in with `az login` and `gh auth login`.
# Safe to rerun.
set -euo pipefail

resource_group="${RESOURCE_GROUP:-rg-hemingford-9901}"
repository="${GITHUB_REPOSITORY:-prabh-62/nl2sql-agent}"
here="$(cd "$(dirname "$0")" && pwd)"

# GitHub's OIDC subject names the repository with immutable IDs, e.g.
# repo:octo-org@123/octo-repo@456:ref:refs/heads/main, so a renamed or recreated repo can't sign in.
subject_repository="$(gh api "repos/$repository" --jq '"\(.owner.login)@\(.owner.id)/\(.name)@\(.id)"')"

outputs="$(az deployment group create \
  --resource-group "$resource_group" \
  --name nl2sql-bootstrap \
  --template-file "$here/main.bicep" \
  --parameters githubSubjectRepository="$subject_repository" \
  --query properties.outputs --output json)"

client_id="$(jq -r .clientId.value <<<"$outputs")"
tenant_id="$(jq -r .tenantId.value <<<"$outputs")"
deployer_client_id="$(jq -r .deployerClientId.value <<<"$outputs")"
subscription_id="$(az account show --query id --output tsv)"

gh variable set AZURE_CLIENT_ID --repo "$repository" --body "$client_id"
gh variable set AZURE_TENANT_ID --repo "$repository" --body "$tenant_id"
gh variable set AZURE_SUBSCRIPTION_ID --repo "$repository" --body "$subscription_id"
gh variable set AZURE_DEPLOYER_CLIENT_ID --repo "$repository" --body "$deployer_client_id"

echo "nl2sql-reader client ID: $client_id"
echo "Set it as ReaderIdentity:ClientId in src/Nl2Sql.AppHost/appsettings.json,"
echo "then run 'aspire run' once so the migration service adds the identity's database user."
