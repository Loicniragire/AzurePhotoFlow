#!/bin/bash

# Kubernetes monitoring and troubleshooting script for AzurePhotoFlow

NAMESPACE="azurephotoflow"

echo "📊 AzurePhotoFlow Kubernetes Monitoring Dashboard"
echo "================================================="

# Function to show section header
show_header() {
    echo ""
    echo "--- $1 ---"
}

# Check namespace exists
if ! kubectl get namespace $NAMESPACE &> /dev/null; then
    echo "❌ Namespace '$NAMESPACE' not found"
    exit 1
fi

# Pod status
show_header "Pod Status"
kubectl get pods -n $NAMESPACE -o wide

# Service status
show_header "Service Status"
kubectl get services -n $NAMESPACE

# Ingress status
show_header "Ingress Status"
kubectl get ingress -n $NAMESPACE

# PVC status
show_header "Persistent Volume Claims"
kubectl get pvc -n $NAMESPACE

# Recent events
show_header "Recent Events"
kubectl get events -n $NAMESPACE --sort-by=.metadata.creationTimestamp | tail -10

# Check for unhealthy pods
show_header "Pod Health Check"
unhealthy_pods=$(kubectl get pods -n $NAMESPACE -o json | jq -r '.items[] | select(.status.phase != "Running" or (.status.containerStatuses[]? | select(.ready != true))) | .metadata.name' 2>/dev/null || true)

if [ -n "$unhealthy_pods" ]; then
    echo "⚠️ Unhealthy pods found:"
    echo "$unhealthy_pods"
    echo ""
    echo "To troubleshoot, run:"
    for pod in $unhealthy_pods; do
        echo "  kubectl describe pod $pod -n $NAMESPACE"
        echo "  kubectl logs $pod -n $NAMESPACE"
    done
else
    echo "✅ All pods are healthy"
fi

# Resource usage (if metrics-server is available)
show_header "Resource Usage"
if kubectl top pods -n $NAMESPACE &> /dev/null; then
    kubectl top pods -n $NAMESPACE
else
    echo "⚠️ Metrics server not available - cannot show resource usage"
fi

# Quick troubleshooting commands
show_header "Troubleshooting Commands"
echo "View logs:"
echo "  kubectl logs -f deployment/backend-deployment -n $NAMESPACE"
echo "  kubectl logs -f deployment/frontend-deployment -n $NAMESPACE"
echo "  kubectl logs -f deployment/minio-deployment -n $NAMESPACE"
echo "  kubectl logs -f deployment/qdrant-deployment -n $NAMESPACE"
echo ""
echo "Port forward for local access:"
echo "  kubectl port-forward service/frontend-service 8080:80 -n $NAMESPACE"
echo "  kubectl port-forward service/backend-service 8081:80 -n $NAMESPACE"
echo "  kubectl port-forward service/minio-service 9000:9000 -n $NAMESPACE"
echo "  kubectl port-forward service/qdrant-service 6333:6333 -n $NAMESPACE"
echo ""
echo "Scale deployments:"
echo "  kubectl scale deployment backend-deployment --replicas=3 -n $NAMESPACE"
echo "  kubectl scale deployment frontend-deployment --replicas=2 -n $NAMESPACE"