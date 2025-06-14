#!/bin/bash

# MicroK8s Deployment Script for AzurePhotoFlow
# Usage: ./scripts/deploy-microk8s.sh [environment] [image-tag]

set -e

ENVIRONMENT=${1:-production}
IMAGE_TAG=${2:-latest}
NAMESPACE="azurephotoflow"

echo "🚀 Deploying AzurePhotoFlow to MicroK8s"
echo "Environment: $ENVIRONMENT"
echo "Image Tag: $IMAGE_TAG"
echo "Namespace: $NAMESPACE"

# Determine kubectl command (prefer regular kubectl if available, fallback to microk8s kubectl)
if command -v kubectl >/dev/null 2>&1 && kubectl get nodes >/dev/null 2>&1; then
    KUBECTL="kubectl"
    echo "✅ Using kubectl"
else
    KUBECTL="microk8s kubectl"
    echo "✅ Using microk8s kubectl"
fi

# Check if MicroK8s is ready
if ! microk8s status --wait-ready --timeout=30 >/dev/null 2>&1; then
    echo "❌ MicroK8s is not ready"
    exit 1
fi

echo "✅ MicroK8s cluster connection verified"

# Function to wait for deployment to be ready
wait_for_deployment() {
    local deployment_name=$1
    echo "⏳ Waiting for deployment $deployment_name to be ready..."
    $KUBECTL wait --for=condition=available --timeout=300s deployment/$deployment_name -n $NAMESPACE
    if [ $? -eq 0 ]; then
        echo "✅ Deployment $deployment_name is ready"
    else
        echo "❌ Deployment $deployment_name failed to become ready"
        exit 1
    fi
}

# Create namespace if it doesn't exist
echo "📁 Creating namespace..."
$KUBECTL apply -f k8s/namespace.yaml

# Deploy secrets (you need to update these manually first)
echo "🔐 Applying secrets..."
$KUBECTL apply -f k8s/secrets.yaml

# Deploy ConfigMap
echo "⚙️ Applying configuration..."
$KUBECTL apply -f k8s/configmap.yaml

# Deploy storage components
echo "💾 Deploying storage components..."
$KUBECTL apply -f k8s/storage/

# Wait for storage components to be ready
wait_for_deployment "minio-deployment"
wait_for_deployment "qdrant-deployment"

# Update image tags in deployments
echo "🏷️ Updating image tags to $IMAGE_TAG..."
sed -i.bak "s/:latest/:$IMAGE_TAG/g" k8s/app/backend-deployment.yaml
sed -i.bak "s/:latest/:$IMAGE_TAG/g" k8s/app/frontend-deployment.yaml

# Deploy application components
echo "🚀 Deploying application components..."
$KUBECTL apply -f k8s/app/

# Wait for application deployments
wait_for_deployment "backend-deployment"
wait_for_deployment "frontend-deployment"

# Deploy ingress (use MicroK8s-specific ingress if it exists)
echo "🌐 Deploying ingress..."
if [ -f "k8s/ingress-microk8s.yaml" ]; then
    echo "Using MicroK8s-specific ingress configuration"
    $KUBECTL apply -f k8s/ingress-microk8s.yaml
else
    echo "Using standard ingress configuration"
    $KUBECTL apply -f k8s/ingress.yaml
fi

# Restore original deployment files
mv k8s/app/backend-deployment.yaml.bak k8s/app/backend-deployment.yaml
mv k8s/app/frontend-deployment.yaml.bak k8s/app/frontend-deployment.yaml

# Show deployment status
echo "📊 Deployment Status:"
$KUBECTL get pods -n $NAMESPACE
echo ""
$KUBECTL get services -n $NAMESPACE
echo ""
$KUBECTL get ingress -n $NAMESPACE

# Get access information
echo ""
echo "🌍 Access Information:"

# Check if MetalLB is enabled for LoadBalancer services
if microk8s status | grep -q "^metallb: enabled"; then
    INGRESS_IP=$($KUBECTL get ingress azurephotoflow-ingress -n $NAMESPACE -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || echo "")
    if [ -n "$INGRESS_IP" ]; then
        echo "External IP (via MetalLB): $INGRESS_IP"
        echo "Application will be available at: https://$INGRESS_IP (once DNS is configured)"
    else
        echo "LoadBalancer IP not yet assigned. Check again in a few minutes:"
        echo "$KUBECTL get ingress azurephotoflow-ingress -n $NAMESPACE"
    fi
else
    echo "⚠️ MetalLB not enabled - using NodePort access"
    NODE_IP=$($KUBECTL get nodes -o jsonpath='{.items[0].status.addresses[?(@.type=="InternalIP")].address}')
    INGRESS_HTTP_PORT=$($KUBECTL get svc -n ingress -o jsonpath='{.items[?(@.metadata.name=="nginx-ingress-microk8s-controller")].spec.ports[?(@.name=="http")].nodePort}' 2>/dev/null || echo "80")
    INGRESS_HTTPS_PORT=$($KUBECTL get svc -n ingress -o jsonpath='{.items[?(@.metadata.name=="nginx-ingress-microk8s-controller")].spec.ports[?(@.name=="https")].nodePort}' 2>/dev/null || echo "443")
    
    echo "Node IP: $NODE_IP"
    echo "HTTP Port: $INGRESS_HTTP_PORT"
    echo "HTTPS Port: $INGRESS_HTTPS_PORT"
    echo ""
    echo "Access URLs (configure your domain to point to $NODE_IP):"
    echo "  HTTP:  http://$NODE_IP:$INGRESS_HTTP_PORT"
    echo "  HTTPS: https://$NODE_IP:$INGRESS_HTTPS_PORT"
fi

echo ""
echo "✅ Deployment completed successfully!"
echo ""
echo "📋 Next steps:"
echo "1. Update DNS records to point your domain to the IP address above"
echo "2. Wait for SSL certificates to be issued (check: $KUBECTL get certificates -n $NAMESPACE)"
echo "3. Test the application endpoints"
echo "4. Monitor logs: $KUBECTL logs -f deployment/backend-deployment -n $NAMESPACE"
echo ""
echo "🔧 Useful commands:"
echo "Monitor status: ./scripts/monitor-k8s.sh"
echo "Check certificates: $KUBECTL describe certificate -n $NAMESPACE"
echo "Port forward for testing: $KUBECTL port-forward service/frontend-service 8080:80 -n $NAMESPACE"