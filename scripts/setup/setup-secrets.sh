#!/bin/bash

# Setup Kubernetes secrets for AzurePhotoFlow
# This script helps you create the necessary secrets manually

set -e

NAMESPACE="azurephotoflow"

echo "🔐 Setting up Kubernetes secrets for AzurePhotoFlow"

# Check if kubectl is available
if ! command -v kubectl &> /dev/null; then
    echo "❌ kubectl is not installed or not in PATH"
    exit 1
fi

# Function to prompt for secret value
prompt_secret() {
    local secret_name=$1
    local description=$2
    echo -n "Enter $description: "
    read -s secret_value
    echo
    echo "$secret_value"
}

# Function to base64 encode
base64_encode() {
    echo -n "$1" | base64 -w 0
}

echo "Please provide the following secret values:"
echo ""

# Collect secrets
VITE_GOOGLE_CLIENT_ID=$(prompt_secret "VITE_GOOGLE_CLIENT_ID" "Google OAuth Client ID")
JWT_SECRET_KEY=$(prompt_secret "JWT_SECRET_KEY" "JWT Secret Key (strong random string)")

# Docker registry credentials
echo ""
echo "Docker Registry Credentials for GHCR:"
GHCR_USERNAME=$(prompt_secret "GHCR_USERNAME" "GitHub username")
GHCR_TOKEN=$(prompt_secret "GHCR_TOKEN" "GitHub Personal Access Token")

echo ""
echo "🏗️ Creating secrets in Kubernetes..."

# Create namespace if it doesn't exist
kubectl create namespace $NAMESPACE --dry-run=client -o yaml | kubectl apply -f -

# Create application secrets
kubectl create secret generic azurephotoflow-secrets \
  --from-literal=VITE_GOOGLE_CLIENT_ID="$VITE_GOOGLE_CLIENT_ID" \
  --from-literal=JWT_SECRET_KEY="$JWT_SECRET_KEY" \
  --from-literal=MINIO_ACCESS_KEY="minioadmin" \
  --from-literal=MINIO_SECRET_KEY="minioadmin" \
  --from-literal=QDRANT_API_KEY="" \
  --namespace=$NAMESPACE \
  --dry-run=client -o yaml | kubectl apply -f -

# Create docker registry secret
kubectl create secret docker-registry registry-secret \
  --docker-server=ghcr.io \
  --docker-username="$GHCR_USERNAME" \
  --docker-password="$GHCR_TOKEN" \
  --namespace=$NAMESPACE \
  --dry-run=client -o yaml | kubectl apply -f -

echo "✅ Secrets created successfully!"
echo ""
echo "📋 Next steps:"
echo "1. Update k8s/configmap.yaml with your domain and environment-specific values"
echo "2. Update k8s/ingress.yaml with your actual domain names"
echo "3. Run the deployment: ./scripts/deploy-k8s.sh"