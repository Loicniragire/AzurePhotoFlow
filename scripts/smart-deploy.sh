#!/bin/bash

# Smart Deployment Script for AzurePhotoFlow
# This script uses cluster configuration analysis to make intelligent deployment decisions

set -e

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

print_status() { echo -e "${BLUE}$1${NC}"; }
print_success() { echo -e "${GREEN}✅ $1${NC}"; }
print_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }
print_error() { echo -e "${RED}❌ $1${NC}"; }
print_info() { echo -e "${CYAN}ℹ️  $1${NC}"; }

# Configuration files
CONFIG_FILE="cluster-config.json"
SSH_ENV_FILE="$HOME/.ssh_env"

# Load SSH environment if available
if [ -f "$SSH_ENV_FILE" ]; then
    source "$SSH_ENV_FILE"
fi

# Check if jq is available for JSON parsing
if ! command -v jq >/dev/null 2>&1; then
    print_warning "jq not found - attempting to install..."
    
    # Try different installation methods
    if command -v brew >/dev/null 2>&1; then
        print_status "Installing jq via Homebrew..."
        brew install jq || {
            print_error "Failed to install jq via brew"
            print_info "Falling back to Python JSON parsing"
            use_python_json=true
        }
    elif command -v apt-get >/dev/null 2>&1; then
        sudo apt-get update && sudo apt-get install -y jq || {
            print_error "Failed to install jq via apt"
            use_python_json=true
        }
    else
        print_warning "No package manager found - using Python fallback"
        use_python_json=true
    fi
fi

# Function to execute remote commands
remote_exec() {
    local command="$1"
    local description="$2"
    
    if [ -n "$description" ]; then
        print_status "$description"
    fi
    
    ssh -o ConnectTimeout=10 -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null \
        -o LogLevel=ERROR "$SSH_USER@$SSH_HOST" "$command"
}

# Function to check if cluster config exists and is valid
check_config_file() {
    if [ ! -f "$CONFIG_FILE" ]; then
        print_error "Configuration file $CONFIG_FILE not found"
        return 1
    fi
    
    if ! jq empty "$CONFIG_FILE" 2>/dev/null; then
        print_error "Configuration file $CONFIG_FILE is not valid JSON"
        return 1
    fi
    
    return 0
}

# Function to get configuration value
get_config() {
    local key="$1"
    
    if [ "$use_python_json" = "true" ]; then
        python3 -c "
import json, sys
try:
    with open('$CONFIG_FILE', 'r') as f:
        data = json.load(f)
    
    # Simple key parsing for our use cases
    if '$key' == '.cluster_ready':
        print(data.get('cluster_ready', 'false'))
    elif '$key' == '.namespaces[\"azurephotoflow\"].exists':
        print(data.get('namespaces', {}).get('azurephotoflow', {}).get('exists', 'false'))
    elif '$key' == '.secrets[\"azurephotoflow-secrets\"]':
        print(data.get('secrets', {}).get('azurephotoflow-secrets', 'false'))
    elif '$key' == '.secrets[\"registry-secret\"]':
        print(data.get('secrets', {}).get('registry-secret', 'false'))
    elif '$key' == '.deployments[\"backend-deployment\"].exists':
        print(data.get('deployments', {}).get('backend-deployment', {}).get('exists', 'false'))
    elif '$key' == '.deployments[\"frontend-deployment\"].exists':
        print(data.get('deployments', {}).get('frontend-deployment', {}).get('exists', 'false'))
    else:
        print('')
except:
    print('')
" 2>/dev/null
    else
        jq -r "$key // empty" "$CONFIG_FILE" 2>/dev/null
    fi
}

# Function to check if action is needed
action_needed() {
    local action="$1"
    
    if [ "$use_python_json" = "true" ]; then
        python3 -c "
import json
try:
    with open('$CONFIG_FILE', 'r') as f:
        data = json.load(f)
    actions = data.get('actions_needed', [])
    for action_item in actions:
        if '$action' in action_item:
            exit(0)
    exit(1)
except:
    exit(1)
" 2>/dev/null
    else
        jq -r '.actions_needed[]? // empty' "$CONFIG_FILE" 2>/dev/null | grep -q "$action"
    fi
}

# Function to apply cluster fixes based on configuration
apply_cluster_fixes() {
    print_status "🔧 Applying cluster fixes based on configuration..."
    
    # Check if MicroK8s needs to be started
    if action_needed "start_microk8s"; then
        print_warning "Starting MicroK8s..."
        remote_exec "microk8s start" "Starting MicroK8s services"
        sleep 10
    fi
    
    # Check if MicroK8s needs to be restarted
    if action_needed "restart_microk8s"; then
        print_warning "Restarting MicroK8s for clean state..."
        remote_exec "microk8s stop || true" "Stopping MicroK8s"
        sleep 5
        remote_exec "microk8s start" "Starting MicroK8s"
        sleep 15
    fi
    
    # Enable missing addons
    local addons_action=$(jq -r '.actions_needed[]? // empty' "$CONFIG_FILE" | grep "enable_addons:" | head -1)
    if [ -n "$addons_action" ]; then
        local addons=$(echo "$addons_action" | cut -d: -f2)
        print_warning "Enabling missing addons: $addons"
        remote_exec "microk8s enable $addons" "Enabling required addons"
        sleep 10
    fi
    
    # Set default storage class if needed
    if action_needed "set_default_storage_class"; then
        print_warning "Setting default storage class..."
        remote_exec "microk8s kubectl patch storageclass microk8s-hostpath -p '{\"metadata\": {\"annotations\":{\"storageclass.kubernetes.io/is-default-class\":\"true\"}}}'" "Setting default storage class"
    fi
}

# Function to create namespace if needed
create_namespace() {
    local namespace="azurephotoflow"
    
    if action_needed "create_namespace:$namespace"; then
        print_status "Creating namespace $namespace..."
        remote_exec "microk8s kubectl create namespace $namespace" "Creating namespace"
    else
        print_info "Namespace $namespace already exists"
    fi
}

# Function to handle secrets intelligently
manage_secrets() {
    local namespace="azurephotoflow"
    print_status "🔐 Managing application secrets..."
    
    # Create application secrets if needed
    if action_needed "create_secret:azurephotoflow-secrets"; then
        print_status "Creating application secrets..."
        remote_exec "microk8s kubectl delete secret azurephotoflow-secrets -n $namespace --ignore-not-found=true" "Cleaning old application secrets"
        
        remote_exec "microk8s kubectl create secret generic azurephotoflow-secrets \
            --from-literal=VITE_GOOGLE_CLIENT_ID='$VITE_GOOGLE_CLIENT_ID' \
            --from-literal=JWT_SECRET_KEY='$JWT_SECRET_KEY' \
            --from-literal=MINIO_ACCESS_KEY='$MINIO_ACCESS_KEY' \
            --from-literal=MINIO_SECRET_KEY='$MINIO_SECRET_KEY' \
            --namespace=$namespace" "Creating application secrets"
    else
        print_success "Application secrets already exist"
    fi
    
    # Create registry secret if needed
    if action_needed "create_secret:registry-secret"; then
        print_status "Creating registry secret..."
        remote_exec "microk8s kubectl delete secret registry-secret -n $namespace --ignore-not-found=true" "Cleaning old registry secret"
        
        remote_exec "microk8s kubectl create secret docker-registry registry-secret \
            --docker-server=ghcr.io \
            --docker-username='$GHCR_USERNAME' \
            --docker-password='$GHCR_TOKEN' \
            --namespace=$namespace" "Creating registry secret"
    else
        print_success "Registry secret already exists"
    fi
}

# Function to deploy application based on recommendations
deploy_application() {
    local namespace="azurephotoflow"
    print_status "🚀 Deploying application based on cluster analysis..."
    
    # Get deployment recommendations
    local recommendations=$(jq -r '.recommendations[]? // empty' "$CONFIG_FILE")
    
    if echo "$recommendations" | grep -q "FULL_DEPLOYMENT"; then
        print_status "Performing full deployment (clean install)..."
        deploy_full_application
    elif echo "$recommendations" | grep -q "UPDATE_DEPLOYMENT"; then
        print_status "Performing rolling update (existing deployment)..."
        update_existing_deployment
    elif echo "$recommendations" | grep -q "PARTIAL_DEPLOYMENT"; then
        print_status "Performing partial deployment (fixing missing components)..."
        deploy_missing_components
    else
        print_warning "No clear deployment recommendation - defaulting to full deployment"
        deploy_full_application
    fi
}

# Function to perform full deployment
deploy_full_application() {
    local namespace="azurephotoflow"
    print_status "Deploying all application components..."
    
    # Copy manifests to remote server
    local deploy_dir="/tmp/k8s-deploy-$(date +%s)"
    remote_exec "mkdir -p $deploy_dir" "Creating deployment directory"
    
    # Use scp to copy files
    scp -o ConnectTimeout=10 -o StrictHostKeyChecking=no -r k8s/ "$SSH_USER@$SSH_HOST:$deploy_dir/"
    
    # Deploy in order
    remote_exec "cd $deploy_dir && microk8s kubectl apply -f namespace.yaml" "Creating namespace"
    remote_exec "cd $deploy_dir && microk8s kubectl apply -f configmap.yaml" "Applying configuration"
    remote_exec "cd $deploy_dir && microk8s kubectl apply -f storage/" "Setting up storage"
    remote_exec "cd $deploy_dir && microk8s kubectl apply -f app/" "Deploying applications"
    remote_exec "cd $deploy_dir && microk8s kubectl apply -f ingress-microk8s.yaml" "Configuring ingress"
    
    # Cleanup
    remote_exec "rm -rf $deploy_dir" "Cleaning up deployment files"
}

# Function to update existing deployment
update_existing_deployment() {
    local namespace="azurephotoflow"
    print_status "Updating existing deployments with new images..."
    
    # Get current image tag
    local image_tag="${BUILD_BUILDID:-latest}"
    local registry="${CONTAINER_REGISTRY:-ghcr.io/loicniragire/photoflow}"
    
    # Update backend deployment
    if [ "$(get_config '.deployments["backend-deployment"].exists')" = "true" ]; then
        remote_exec "microk8s kubectl set image deployment/backend-deployment backend=$registry/azurephotoflow-backend:$image_tag -n $namespace" "Updating backend image"
    fi
    
    # Update frontend deployment
    if [ "$(get_config '.deployments["frontend-deployment"].exists')" = "true" ]; then
        remote_exec "microk8s kubectl set image deployment/frontend-deployment frontend=$registry/azurephotoflow-frontend:$image_tag -n $namespace" "Updating frontend image"
    fi
    
    print_success "Image updates applied"
}

# Function to deploy missing components
deploy_missing_components() {
    local namespace="azurephotoflow"
    print_status "Deploying missing components..."
    
    # Check each component and deploy if missing
    local components=("minio-deployment" "qdrant-deployment" "backend-deployment" "frontend-deployment")
    
    for component in "${components[@]}"; do
        if [ "$(get_config ".deployments[\"$component\"].exists")" != "true" ]; then
            print_status "Deploying missing component: $component"
            # Deploy specific component - this would need component-specific logic
            deploy_full_application  # For now, fall back to full deployment
            break
        fi
    done
}

# Function to wait for deployments
wait_for_deployments() {
    local namespace="azurephotoflow"
    print_status "⏳ Waiting for deployments to be ready..."
    
    local deployments=("minio-deployment" "qdrant-deployment" "backend-deployment" "frontend-deployment")
    
    for deployment in "${deployments[@]}"; do
        print_status "Waiting for $deployment..."
        if remote_exec "microk8s kubectl wait --for=condition=available --timeout=300s deployment/$deployment -n $namespace" ""; then
            print_success "$deployment is ready"
        else
            print_warning "$deployment took longer than expected"
        fi
    done
}

# Function to verify deployment
verify_deployment() {
    local namespace="azurephotoflow"
    print_status "🔍 Verifying deployment..."
    
    # Show pod status
    remote_exec "microk8s kubectl get pods -n $namespace" "Pod status:"
    
    # Check service endpoints
    remote_exec "microk8s kubectl get services -n $namespace" "Service status:"
    
    # Check ingress
    remote_exec "microk8s kubectl get ingress -n $namespace" "Ingress status:"
    
    # Test backend health (if available)
    print_status "Testing backend health..."
    for i in {1..3}; do
        if remote_exec "microk8s kubectl exec -n $namespace deployment/backend-deployment -- curl -f http://localhost:8080/health" "" 2>/dev/null; then
            print_success "Backend health check passed"
            break
        else
            print_warning "Backend health check attempt $i/3 failed"
            sleep 10
        fi
    done
}

# Main deployment function
main() {
    print_status "🚀 Starting smart deployment process..."
    
    # Validate configuration
    if ! check_config_file; then
        print_error "Configuration file validation failed"
        exit 1
    fi
    
    # Check if cluster is ready
    local cluster_ready=$(get_config '.cluster_ready')
    if [ "$cluster_ready" != "true" ]; then
        print_warning "Cluster is not ready - applying fixes..."
        apply_cluster_fixes
    else
        print_success "Cluster is ready for deployment"
    fi
    
    # Create namespace if needed
    create_namespace
    
    # Manage secrets
    manage_secrets
    
    # Deploy application
    deploy_application
    
    # Wait for deployments
    wait_for_deployments
    
    # Verify deployment
    verify_deployment
    
    print_success "🎉 Smart deployment completed successfully!"
    
    # Show access information
    local domain="${PRODUCTION_DOMAIN:-localhost}"
    echo ""
    print_info "Access your application:"
    print_info "  Frontend: https://$domain"
    print_info "  API: https://$domain/api"
    print_info "  Health: https://$domain/api/health"
}

# Export functions for external use
export -f remote_exec get_config action_needed

# Run main function if script is executed directly
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi