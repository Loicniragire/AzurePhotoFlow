#!/bin/bash
# check_image_pull_extended.sh
#
# This script verifies that the Web App attempted to pull container images by examining
# its logs for keywords indicating container image processing. It runs the log tail command
# in the background for a fixed period, then checks the collected logs.
#
# Usage: ./check_image_pull_extended.sh
#
WEBAPP="AzurePhotoFlowWebApp"
RG="AzurePhotoFlow-RG"
TIMEOUT_SECONDS=120
LOG_FILE="webapp_logs_extended.txt"

echo "Starting to tail Web App logs for $TIMEOUT_SECONDS seconds to check for image pull events..."
rm -f "$LOG_FILE"

az webapp log tail --name "$WEBAPP" --resource-group "$RG" > "$LOG_FILE" 2>&1 &
TAIL_PID=$!

sleep "$TIMEOUT_SECONDS"

kill "$TAIL_PID" 2>/dev/null

echo "Finished tailing logs. Searching for image pull events..."
if grep -iE "pull|download|fetch|start|Initializing" "$LOG_FILE"; then
  echo "Evidence of image pull events detected in container logs."
else
  echo "No clear evidence of image pull events was found in the logs within $TIMEOUT_SECONDS seconds."
fi

