#!/bin/bash

# Kubernetes Deployment Script for AzurePhotoFlow
# Usage: ./scripts/deploy-k8s.sh [environment] [image-tag]

set -e

ENVIRONMENT=${1:-production}
IMAGE_TAG=${2:-latest}
NAMESPACE="azurephotoflow"

echo "🚀 Deploying AzurePhotoFlow to Kubernetes"
echo "Environment: $ENVIRONMENT"
echo "Image Tag: $IMAGE_TAG"
echo "Namespace: $NAMESPACE"

# Check if kubectl is available
if ! command -v kubectl &> /dev/null; then
    echo "❌ kubectl is not installed or not in PATH"
    exit 1
fi

# Check if we can connect to the cluster
if ! kubectl cluster-info &> /dev/null; then
    echo "❌ Cannot connect to Kubernetes cluster"
    exit 1
fi

echo "✅ Kubernetes cluster connection verified"

# Function to wait for deployment to be ready
wait_for_deployment() {
    local deployment_name=$1
    echo "⏳ Waiting for deployment $deployment_name to be ready..."
    kubectl wait --for=condition=available --timeout=300s deployment/$deployment_name -n $NAMESPACE
    if [ $? -eq 0 ]; then
        echo "✅ Deployment $deployment_name is ready"
    else
        echo "❌ Deployment $deployment_name failed to become ready"
        exit 1
    fi
}

# Create namespace if it doesn't exist
echo "📁 Creating namespace..."
kubectl apply -f k8s/namespace.yaml

# Deploy secrets (you need to update these manually first)
echo "🔐 Applying secrets..."
kubectl apply -f k8s/secrets.yaml

# Deploy ConfigMap
echo "⚙️ Applying configuration..."
kubectl apply -f k8s/configmap.yaml

# Deploy storage components
echo "💾 Deploying storage components..."
kubectl apply -f k8s/storage/

# Wait for storage components to be ready
wait_for_deployment "minio-deployment"
wait_for_deployment "qdrant-deployment"

# Update image tags in deployments
echo "🏷️ Updating image tags to $IMAGE_TAG..."
sed -i.bak "s/:latest/:$IMAGE_TAG/g" k8s/app/backend-deployment.yaml
sed -i.bak "s/:latest/:$IMAGE_TAG/g" k8s/app/frontend-deployment.yaml

# Deploy application components
echo "🚀 Deploying application components..."
kubectl apply -f k8s/app/

# Wait for application deployments
wait_for_deployment "backend-deployment"
wait_for_deployment "frontend-deployment"

# Deploy ingress
echo "🌐 Deploying ingress..."
kubectl apply -f k8s/ingress.yaml

# Restore original deployment files
mv k8s/app/backend-deployment.yaml.bak k8s/app/backend-deployment.yaml
mv k8s/app/frontend-deployment.yaml.bak k8s/app/frontend-deployment.yaml

# Show deployment status
echo "📊 Deployment Status:"
kubectl get pods -n $NAMESPACE
echo ""
kubectl get services -n $NAMESPACE
echo ""
kubectl get ingress -n $NAMESPACE

# Get external IP/URL
echo ""
echo "🌍 Access URLs:"
INGRESS_IP=$(kubectl get ingress azurephotoflow-ingress -n $NAMESPACE -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
if [ -n "$INGRESS_IP" ]; then
    echo "External IP: $INGRESS_IP"
    echo "Update your DNS to point your domain to this IP"
else
    echo "Ingress IP not yet assigned. Check again in a few minutes:"
    echo "kubectl get ingress azurephotoflow-ingress -n $NAMESPACE"
fi

echo ""
echo "✅ Deployment completed successfully!"
echo ""
echo "📋 Next steps:"
echo "1. Update DNS records to point to the ingress IP"
echo "2. Verify SSL certificates are issued (if using cert-manager)"
echo "3. Test the application endpoints"
echo "4. Monitor logs: kubectl logs -f deployment/backend-deployment -n $NAMESPACE"