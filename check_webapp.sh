#!/bin/bash
# check_webapp.sh
#
# This script uses Azure CLI commands to verify the configuration and status of your Azure Web App.
# It performs the following checks:
#   - Confirms that the App Service is running.
#   - Verifies that the default hostname (FQDN) of the App Service matches the expected value.
#   - Checks that the /health endpoint (as served via your Nginx reverse proxy in the container) is healthy.
#   - Validates that the Docker Compose configuration is correctly applied and that the images are pulled.
#
# If the /health endpoint fails or the Docker Compose configuration appears not to have been applied,
# the script performs additional diagnostics (verbose curl and log tailing).
#
# Usage:
#   ./check_webapp.sh <ResourceGroup> <AppServiceName> <ExpectedFQDN> <ExpectedDockerComposeURL>
#
# Example:
#   ./check_webapp.sh AzurePhotoFlow-RG azurephotoflowwebapp azurephotoflowwebapp.azurewebsites.net "COMPOSE|https://photoflowtfstatedev.blob.core.windows.net/docker-compose/docker-compose.yml"

if [ "$#" -ne 4 ]; then
  echo "Usage: $0 <ResourceGroup> <AppServiceName> <ExpectedFQDN> <ExpectedDockerComposeURL>"
  exit 1
fi

# Assign script arguments to variables
RG=$1
APPSERVICE=$2
EXPECTED_HOST=$3
EXPECTED_COMPOSE_URL=$4

# Ensure jq is installed for JSON parsing
if ! command -v jq &>/dev/null; then
  echo "Error: jq is required but not installed. Please install jq and re-run the script."
  exit 1
fi

issues_found=0

echo "---------------------------------------------"
echo "Checking App Service Status..."
app_status_json=$(az webapp show --name "$APPSERVICE" --resource-group "$RG" --output json)
if [ $? -ne 0 ]; then
  echo "Error retrieving App Service details."
  issues_found=1
else
  state=$(echo "$app_status_json" | jq -r '.state')
  echo "App Service State: $state"
  if [ "$state" != "Running" ]; then
    echo "Issue: App Service is not running (state: $state)."
    issues_found=1
  else
    echo "App Service is running."
  fi
fi

echo "---------------------------------------------"
echo "Checking App Service FQDN..."
app_fqdn=$(az webapp show --name "$APPSERVICE" --resource-group "$RG" --query defaultHostName --output tsv)
if [ $? -ne 0 ]; then
  echo "Error retrieving App Service FQDN."
  issues_found=1
else
  echo "App Service FQDN: $app_fqdn"
  if [ "$app_fqdn" != "$EXPECTED_HOST" ]; then
    echo "Issue: App Service FQDN ($app_fqdn) does not match expected host ($EXPECTED_HOST)."
    issues_found=1
  else
    echo "App Service FQDN matches expected host."
  fi
fi

echo "---------------------------------------------"
echo "Checking /health endpoint..."
# Define maximum attempts and delay between attempts.
MAX_ATTEMPTS=6
SLEEP_BETWEEN=10
ATTEMPT=1
HEALTH_URL="https://$EXPECTED_HOST/health"

while [ $ATTEMPT -le $MAX_ATTEMPTS ]; do
  echo "Attempt $ATTEMPT: Checking $HEALTH_URL..."
  # Use curl to check the health endpoint; -s for silent, -f to fail on HTTP errors.
  if curl -s -f "$HEALTH_URL"; then
    echo "/health endpoint is healthy."
    break
  else
    echo "Health check failed. Waiting $SLEEP_BETWEEN seconds before retry..."
    ATTEMPT=$((ATTEMPT+1))
    sleep $SLEEP_BETWEEN
  fi
done

if [ $ATTEMPT -gt $MAX_ATTEMPTS ]; then
  echo "Issue: /health endpoint did not return a healthy status after $MAX_ATTEMPTS attempts."
  issues_found=1
  
  echo "---------------------------------------------"
  echo "Running verbose curl for additional details..."
  curl -v "$HEALTH_URL"
fi

echo "---------------------------------------------"
echo "Validating Docker Compose configuration..."
# Retrieve the container configuration and extract the DOCKER_CUSTOM_IMAGE_NAME value.
container_config=$(az webapp config container show --name "$APPSERVICE" --resource-group "$RG" --output json)
compose_value=$(echo "$container_config" | jq -r '.[] | select(.name=="DOCKER_CUSTOM_IMAGE_NAME") | .value')

echo "Configured Docker Compose value: $compose_value"
if [ "$compose_value" == "$EXPECTED_COMPOSE_URL" ]; then
  echo "Configuration check passed: Docker Compose file reference is correctly set."
else
  echo "Issue: Expected Docker Compose reference: $EXPECTED_COMPOSE_URL, but got: $compose_value"
  issues_found=1
fi

echo "---------------------------------------------"
echo "Tailing container logs for evidence of Docker Compose pull..."
# Tail the logs for 30 seconds and filter for keywords indicating that the compose file was processed.
echo "Searching for 'docker-compose' in log stream:"
timeout 30 az webapp log tail --name "$APPSERVICE" --resource-group "$RG" | grep -i "docker-compose"

echo "---------------------------------------------"
echo "Summary of Potential Issues:"
if [ "$issues_found" -eq 0 ]; then
  echo "No issues detected. The App Service configuration, Docker Compose reference, and health endpoint are as expected."
else
  echo "One or more issues were detected. Please review the above output for details."
fi

exit $issues_found

