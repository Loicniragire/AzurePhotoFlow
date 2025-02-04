#!/bin/bash
# check_agw_backend.sh
#
# This script uses Azure CLI commands to verify the configuration of your Application Gateway
# and its connection to your backend App Service. It checks:
#   - The probe's host setting.
#   - The backend HTTP settings (hostName).
#   - The overall backend health (ensuring all endpoints are healthy).
#   - The App Service's default hostname.
#
# Usage:
#   ./check_agw_backend.sh <ResourceGroup> <GatewayName> <ProbeName> <HTTPSettingsName> <AppServiceName> <ExpectedFQDN>
#
# Example:
#   ./check_agw_backend.sh AzurePhotoFlow-RG AzurePhotoFlowAG health_probe backend_http_settings azurephotoflowwebapp azurephotoflowwebapp.azurewebsites.net

if [ "$#" -ne 6 ]; then
  echo "Usage: $0 <ResourceGroup> <GatewayName> <ProbeName> <HTTPSettingsName> <AppServiceName> <ExpectedFQDN>"
  exit 1
fi

# Assign script arguments to variables
RG=$1
GW=$2
PROBE=$3
HTTPSETTINGS=$4
APPSERVICE=$5
EXPECTED_HOST=$6

# Ensure jq is installed for JSON parsing
if ! command -v jq &>/dev/null; then
  echo "Error: jq is required but not installed. Please install jq and re-run the script."
  exit 1
fi

issues_found=0

echo "---------------------------------------------"
echo "Checking Application Gateway Probe configuration..."
probe_json=$(az network application-gateway probe show \
  --resource-group "$RG" \
  --gateway-name "$GW" \
  --name "$PROBE" \
  --output json)

if [ $? -ne 0 ]; then
  echo "Error retrieving probe configuration."
  issues_found=1
else
  probe_host=$(echo "$probe_json" | jq -r '.host')
  echo "Probe Host: $probe_host"
  if [ "$probe_host" != "$EXPECTED_HOST" ]; then
    echo "Issue: Probe host ($probe_host) does not match expected host ($EXPECTED_HOST)."
    issues_found=1
  else
    echo "Probe host matches expected host."
  fi
fi

echo "---------------------------------------------"
echo "Checking Backend HTTP Settings..."
http_json=$(az network application-gateway http-settings show \
  --resource-group "$RG" \
  --gateway-name "$GW" \
  --name "$HTTPSETTINGS" \
  --output json)

if [ $? -ne 0 ]; then
  echo "Error retrieving HTTP settings."
  issues_found=1
else
  http_hostname=$(echo "$http_json" | jq -r '.hostName')
  echo "HTTP Settings hostName: $http_hostname"
  if [ "$http_hostname" != "$EXPECTED_HOST" ]; then
    echo "Issue: HTTP settings hostName ($http_hostname) does not match expected host ($EXPECTED_HOST)."
    issues_found=1
  else
    echo "HTTP settings hostName matches expected host."
  fi
fi

echo "---------------------------------------------"
echo "Checking Backend Health..."
health_json=$(az network application-gateway show-backend-health \
  --resource-group "$RG" \
  --name "$GW" \
  --output json)

if [ $? -ne 0 ]; then
  echo "Error retrieving backend health."
  issues_found=1
else
  # Count endpoints that are not healthy
  unhealthy_count=$(echo "$health_json" | jq '[.backendAddressPools[].backendHttpSettings[].servers[] | select(.health != "Healthy")] | length')
  total_count=$(echo "$health_json" | jq '[.backendAddressPools[].backendHttpSettings[].servers[]] | length')
  echo "Total backend endpoints: $total_count, Unhealthy endpoints: $unhealthy_count"
  if [ "$unhealthy_count" -gt 0 ]; then
    echo "Issue: Some backend endpoints are not healthy. Details:"
    echo "$health_json" | jq '.backendAddressPools[].backendHttpSettings[].servers[] | select(.health != "Healthy")'
    issues_found=1
  else
    echo "All backend endpoints are healthy."
  fi
fi

echo "---------------------------------------------"
echo "Checking App Service FQDN..."
app_fqdn=$(az webapp show \
  --name "$APPSERVICE" \
  --resource-group "$RG" \
  --query defaultHostName \
  --output tsv)

if [ $? -ne 0 ]; then
  echo "Error retrieving App Service details."
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
echo "Summary of Potential Issues:"
if [ "$issues_found" -eq 0 ]; then
  echo "No issues detected. All configurations match the expected settings."
else
  echo "One or more issues were detected. Please review the above output for details."
fi

exit $issues_found

