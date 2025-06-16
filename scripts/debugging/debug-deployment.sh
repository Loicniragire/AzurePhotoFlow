#!/bin/bash

# Deployment Debugging Script for AzurePhotoFlow
# This script helps diagnose why the deployment failed

set -e

# Color codes
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

print_status() { echo -e "${BLUE}$1${NC}"; }
print_success() { echo -e "${GREEN}✅ $1${NC}"; }
print_error() { echo -e "${RED}❌ $1${NC}"; }
print_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }
print_info() { echo -e "${CYAN}ℹ️  $1${NC}"; }

# Connection details
SSH_HOST=${SSH_HOST:-"10.0.0.2"}
SSH_USER=${SSH_USER:-"loicn"}
NAMESPACE="azurephotoflow"

print_status "🔍 Debugging AzurePhotoFlow deployment on $SSH_USER@$SSH_HOST"
echo ""

# Function to run remote commands with timeout
remote_check() {
    local cmd="$1"
    local description="$2"
    print_status "$description"
    ssh -o ConnectTimeout=10 -o ServerAliveInterval=10 -o ServerAliveCountMax=2 \
        "$SSH_USER@$SSH_HOST" "$cmd" 2>/dev/null || echo "Command failed or timed out"
    echo ""
}

# 1. Check namespace
print_status "1. Checking namespace status..."
remote_check "microk8s kubectl get namespace $NAMESPACE -o wide" "Namespace details:"

# 2. Check all resources
print_status "2. Checking all resources in namespace..."
remote_check "microk8s kubectl get all -n $NAMESPACE" "All resources:"

# 3. Check secrets
print_status "3. Checking secrets..."
remote_check "microk8s kubectl get secrets -n $NAMESPACE" "Secrets:"

# 4. Check events for errors
print_status "4. Checking recent events..."
remote_check "microk8s kubectl get events -n $NAMESPACE --sort-by='.lastTimestamp'" "Events:"

# 5. Check if manifests exist locally
print_status "5. Checking if Kubernetes manifests exist..."
if [ -d "k8s" ]; then
    print_success "k8s directory found"
    ls -la k8s/
    echo ""
    
    if [ -d "k8s/app" ]; then
        print_success "k8s/app directory found"
        ls -la k8s/app/
    else
        print_error "k8s/app directory missing"
    fi
    
    if [ -d "k8s/storage" ]; then
        print_success "k8s/storage directory found" 
        ls -la k8s/storage/
    else
        print_error "k8s/storage directory missing"
    fi
else
    print_error "k8s directory not found in current directory"
    print_info "Current directory: $(pwd)"
    print_info "Contents:"
    ls -la
fi

# 6. Check if smart-deploy created cluster-config.json
print_status "6. Checking smart deployment configuration..."
if [ -f "cluster-config.json" ]; then
    print_success "cluster-config.json found"
    if command -v jq >/dev/null 2>&1; then
        echo "Cluster ready: $(jq -r '.cluster_ready // "unknown"' cluster-config.json)"
        echo "Actions needed: $(jq -r '.actions_needed[]? // "none"' cluster-config.json)"
        echo "Recommendations: $(jq -r '.recommendations[]? // "none"' cluster-config.json)"
    else
        print_info "Install jq to see detailed config: brew install jq"
    fi
else
    print_error "cluster-config.json not found - smart deployment may not have run"
fi

# 7. Test manual deployment
print_status "7. Testing manual namespace and secret creation..."
if remote_check "microk8s kubectl get namespace $NAMESPACE" "" | grep -q "$NAMESPACE"; then
    print_success "Namespace exists"
else
    print_warning "Creating namespace manually..."
    remote_check "microk8s kubectl create namespace $NAMESPACE" "Creating namespace:"
fi

# 8. Check storage classes
print_status "8. Checking storage classes..."
remote_check "microk8s kubectl get storageclass" "Storage classes:"

# 9. Check node resources
print_status "9. Checking node resources..."
remote_check "microk8s kubectl top nodes" "Node resource usage:"
remote_check "microk8s kubectl describe nodes" "Node details (last 20 lines):" | tail -20

# 10. Check addons
print_status "10. Checking MicroK8s addons..."
remote_check "microk8s status --format short" "Addon status:"

print_status "🎯 Debugging Summary:"
print_info "If no resources are found in the namespace, the deployment likely failed silently."
print_info "Common causes:"
print_info "  1. Missing Kubernetes manifests (k8s/app/, k8s/storage/)"
print_info "  2. Image pull failures (registry authentication)"
print_info "  3. Resource constraints (insufficient memory/CPU)"
print_info "  4. Missing secrets or configuration"
print_info "  5. Storage class issues"
echo ""
print_info "Next steps:"
print_info "  1. Check the Events section above for error messages"
print_info "  2. Verify k8s manifests exist and are valid"
print_info "  3. Try manual deployment: kubectl apply -f k8s/"
print_info "  4. Check pod logs if any pods exist"