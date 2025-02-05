#!/bin/bash
# check_acr_pull_permissions.sh
#
# This script verifies that an Azure Web App (using a managed identity) has the "AcrPull" role 
# assigned on a specified Azure Container Registry (ACR).
#
# Usage:
#   ./check_acr_pull_permissions.sh <ResourceGroup> <WebAppName> <ACRName>
#
# Example:
#   ./check_acr_pull_permissions.sh AzurePhotoFlow-RG azurephotoflowwebapp azurephotoflowacr

if [ "$#" -ne 3 ]; then
  echo "Usage: $0 <ResourceGroup> <WebAppName> <ACRName>"
  exit 1
fi

RG=$1
WEBAPP=$2
ACR_NAME=$3

echo "Retrieving managed identity principal ID for Web App '$WEBAPP' in resource group '$RG'..."
WEBAPP_ID=$(az webapp show --name "$WEBAPP" --resource-group "$RG" --query identity.principalId --output tsv)

if [ -z "$WEBAPP_ID" ]; then
  echo "Error: Managed identity not enabled for Web App '$WEBAPP'."
  exit 1
fi

echo "Managed Identity Principal ID: $WEBAPP_ID"

echo "Retrieving resource ID for ACR '$ACR_NAME' in resource group '$RG'..."
ACR_ID=$(az acr show --name "$ACR_NAME" --resource-group "$RG" --query id --output tsv)

if [ -z "$ACR_ID" ]; then
  echo "Error: Could not retrieve resource ID for ACR '$ACR_NAME'."
  exit 1
fi

echo "ACR Resource ID: $ACR_ID"

echo "Checking if the AcrPull role is assigned to the Web App's managed identity on the ACR..."
ROLE_ASSIGNMENTS=$(az role assignment list --assignee "$WEBAPP_ID" --scope "$ACR_ID" --query "[?roleDefinitionName=='AcrPull']" --output json)

if [ "$(echo "$ROLE_ASSIGNMENTS" | jq 'length')" -gt 0 ]; then
  echo "Success: The Web App's managed identity has the 'AcrPull' role assigned on the ACR."
else
  echo "Error: The Web App's managed identity does not have the 'AcrPull' role assigned on the ACR."
  echo "You can assign it using the following command:"
  echo "az role assignment create --assignee $WEBAPP_ID --role AcrPull --scope $ACR_ID"
  exit 1
fi

echo "Permission verification complete."

