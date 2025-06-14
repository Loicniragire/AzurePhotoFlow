#!/bin/bash

# Remote Kubernetes deployment script for AzurePhotoFlow
# This script deploys to a remote MicroK8s cluster via SSH
# Usage: ./scripts/deploy-k8s-remote.sh [environment] [image-tag] [ssh-options]

set -e

# Default values
ENVIRONMENT=${1:-production}
IMAGE_TAG=${2:-latest}
NAMESPACE="azurephotoflow"

# SSH connection settings
SSH_USER=${SSH_USER:-""}
SSH_HOST=${SSH_HOST:-""}
SSH_PORT=${SSH_PORT:-22}
SSH_KEY=${SSH_KEY:-""}
SSH_OPTIONS="-o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR -o ControlMaster=auto -o ControlPath=~/.ssh/control-%r@%h:%p -o ControlPersist=10m"

# Color codes
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

print_status() { echo -e "${BLUE}$1${NC}"; }
print_success() { echo -e "${GREEN}✅ $1${NC}"; }
print_error() { echo -e "${RED}❌ $1${NC}"; }
print_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }

# Function to show usage
show_usage() {
    echo "Usage: $0 [environment] [image-tag] [OPTIONS]"
    echo ""
    echo "Arguments:"
    echo "  environment           Deployment environment (default: production)"
    echo "  image-tag            Docker image tag (default: latest)"
    echo ""
    echo "Options:"
    echo "  -h, --host HOST       Remote server hostname/IP"
    echo "  -u, --user USER       SSH username"
    echo "  -p, --port PORT       SSH port (default: 22)"
    echo "  -k, --key KEY_PATH    SSH private key path"
    echo "  --help                Show this help message"
    echo ""
    echo "Environment variables:"
    echo "  SSH_HOST              Remote server hostname/IP"
    echo "  SSH_USER              SSH username"
    echo "  SSH_PORT              SSH port (default: 22)"
    echo "  SSH_KEY               SSH private key path"
    echo ""
    echo "Examples:"
    echo "  $0 production v1.2.3 -h 192.168.1.100 -u ubuntu"
    echo "  SSH_HOST=k8s-server.local SSH_USER=admin $0"
}

# Parse additional command line arguments (after environment and image-tag)
shift 2 2>/dev/null || true  # Remove first two args if they exist
while [[ $# -gt 0 ]]; do
    case $1 in
        -h|--host)
            SSH_HOST="$2"
            shift 2
            ;;
        -u|--user)
            SSH_USER="$2"
            shift 2
            ;;
        -p|--port)
            SSH_PORT="$2"
            shift 2
            ;;
        -k|--key)
            SSH_KEY="$2"
            shift 2
            ;;
        --help)
            show_usage
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Validate SSH parameters
if [ -z "$SSH_HOST" ] || [ -z "$SSH_USER" ]; then
    print_error "SSH host and user are required"
    echo "Set SSH_HOST and SSH_USER environment variables or use -h and -u options"
    show_usage
    exit 1
fi

# Build SSH command
SSH_CMD="ssh"
if [ -n "$SSH_KEY" ]; then
    SSH_CMD="$SSH_CMD -i $SSH_KEY"
fi
SSH_CMD="$SSH_CMD $SSH_OPTIONS -p $SSH_PORT $SSH_USER@$SSH_HOST"

# Function to execute remote commands
remote_exec() {
    local command="$1"
    local description="$2"
    
    if [ -n "$description" ]; then
        print_status "$description"
    fi
    
    $SSH_CMD "$command"
}

# Function to copy files to remote server
remote_copy() {
    local local_path="$1"
    local remote_path="$2"
    local description="$3"
    
    if [ -n "$description" ]; then
        print_status "$description"
    fi
    
    local SCP_CMD="scp"
    if [ -n "$SSH_KEY" ]; then
        SCP_CMD="$SCP_CMD -i $SSH_KEY"
    fi
    SCP_CMD="$SCP_CMD $SSH_OPTIONS -P $SSH_PORT"
    
    if [ -d "$local_path" ]; then
        $SCP_CMD -r "$local_path" "$SSH_USER@$SSH_HOST:$remote_path"
    else
        $SCP_CMD "$local_path" "$SSH_USER@$SSH_HOST:$remote_path"
    fi
}

# Function to wait for deployment to be ready
wait_for_deployment() {
    local deployment_name=$1
    echo "⏳ Waiting for deployment $deployment_name to be ready..."
    
    if remote_exec "microk8s kubectl wait --for=condition=available --timeout=300s deployment/$deployment_name -n $NAMESPACE" ""; then
        print_success "Deployment $deployment_name is ready"
    else
        print_error "Deployment $deployment_name failed to become ready"
        # Show pod status for debugging
        echo "Pod status:"
        remote_exec "microk8s kubectl get pods -n $NAMESPACE | grep $deployment_name" ""
        exit 1
    fi
}

echo "🚀 Deploying AzurePhotoFlow to Remote MicroK8s Cluster"
echo "Environment: $ENVIRONMENT"
echo "Image Tag: $IMAGE_TAG"
echo "Namespace: $NAMESPACE"
echo "Remote Server: $SSH_USER@$SSH_HOST:$SSH_PORT"
echo ""

# Check SSH connectivity
print_status "🔌 Testing SSH connection..."
if ! remote_exec "echo 'SSH connection successful'" ""; then
    print_error "Cannot establish SSH connection to $SSH_USER@$SSH_HOST"
    exit 1
fi
print_success "SSH connection established"

# Check if MicroK8s is ready on remote server
print_status "🔍 Checking remote MicroK8s status..."
if ! remote_exec "microk8s status --wait-ready --timeout=30 >/dev/null 2>&1" ""; then
    print_error "MicroK8s is not ready on remote server"
    echo "Start MicroK8s: ssh $SSH_USER@$SSH_HOST 'microk8s start'"
    exit 1
fi
print_success "Remote MicroK8s cluster is ready"

# Create remote deployment directory
REMOTE_DEPLOY_DIR="/tmp/azurephotoflow-deploy-$(date +%s)"
remote_exec "mkdir -p $REMOTE_DEPLOY_DIR" "📁 Creating remote deployment directory..."

# Copy k8s manifests to remote server
print_status "📤 Copying Kubernetes manifests to remote server..."
remote_copy "k8s/" "$REMOTE_DEPLOY_DIR/" "Uploading k8s directory..."

# Copy any additional scripts that might be needed
if [ -f "scripts/monitor-k8s.sh" ]; then
    remote_copy "scripts/monitor-k8s.sh" "$REMOTE_DEPLOY_DIR/" "Uploading monitoring script..."
fi

# Create namespace if it doesn't exist
print_status "📁 Creating namespace..."
remote_exec "cd $REMOTE_DEPLOY_DIR && microk8s kubectl apply -f k8s/namespace.yaml" ""

# Check if secrets exist, if not provide guidance
print_status "🔐 Checking secrets..."
if ! remote_exec "microk8s kubectl get secret azurephotoflow-secrets -n $NAMESPACE >/dev/null 2>&1" ""; then
    print_warning "Secrets not found. You need to create them first."
    echo ""
    echo "Create secrets on remote server:"
    echo "ssh $SSH_USER@$SSH_HOST"
    echo "Then run: ./scripts/setup-secrets.sh"
    echo "Or create manually with kubectl commands"
    echo ""
    read -p "Continue anyway? (y/N): " continue_deploy
    if [[ ! $continue_deploy =~ ^[Yy]$ ]]; then
        print_status "Deployment cancelled. Set up secrets first."
        exit 1
    fi
else
    print_success "Secrets found"
fi

# Deploy secrets and configuration
print_status "⚙️ Applying configuration..."
remote_exec "cd $REMOTE_DEPLOY_DIR && microk8s kubectl apply -f k8s/secrets.yaml" "" || print_warning "Secrets apply failed (may already exist)"
remote_exec "cd $REMOTE_DEPLOY_DIR && microk8s kubectl apply -f k8s/configmap.yaml" ""

# Deploy storage components
print_status "💾 Deploying storage components..."
remote_exec "cd $REMOTE_DEPLOY_DIR && microk8s kubectl apply -f k8s/storage/" ""

# Wait for storage components to be ready
wait_for_deployment "minio-deployment"
wait_for_deployment "qdrant-deployment"

# Update image tags in deployments (create modified versions)
print_status "🏷️ Updating image tags to $IMAGE_TAG..."
remote_exec "cd $REMOTE_DEPLOY_DIR && sed 's/:latest/:$IMAGE_TAG/g' k8s/app/backend-deployment.yaml > k8s/app/backend-deployment-tagged.yaml" ""
remote_exec "cd $REMOTE_DEPLOY_DIR && sed 's/:latest/:$IMAGE_TAG/g' k8s/app/frontend-deployment.yaml > k8s/app/frontend-deployment-tagged.yaml" ""

# Deploy application components
print_status "🚀 Deploying application components..."
remote_exec "cd $REMOTE_DEPLOY_DIR && microk8s kubectl apply -f k8s/app/backend-deployment-tagged.yaml" ""
remote_exec "cd $REMOTE_DEPLOY_DIR && microk8s kubectl apply -f k8s/app/frontend-deployment-tagged.yaml" ""
remote_exec "cd $REMOTE_DEPLOY_DIR && microk8s kubectl apply -f k8s/app/backend-service.yaml" "" || true
remote_exec "cd $REMOTE_DEPLOY_DIR && microk8s kubectl apply -f k8s/app/frontend-service.yaml" "" || true
remote_exec "cd $REMOTE_DEPLOY_DIR && microk8s kubectl apply -f k8s/app/backend-hpa.yaml" "" || true
remote_exec "cd $REMOTE_DEPLOY_DIR && microk8s kubectl apply -f k8s/app/frontend-hpa.yaml" "" || true

# Wait for application deployments
wait_for_deployment "backend-deployment"
wait_for_deployment "frontend-deployment"

# Deploy ingress (prefer MicroK8s-specific version)
print_status "🌐 Deploying ingress..."
if remote_exec "cd $REMOTE_DEPLOY_DIR && test -f k8s/ingress-microk8s.yaml" ""; then
    print_status "Using MicroK8s-specific ingress configuration"
    remote_exec "cd $REMOTE_DEPLOY_DIR && microk8s kubectl apply -f k8s/ingress-microk8s.yaml" ""
else
    print_status "Using standard ingress configuration"
    remote_exec "cd $REMOTE_DEPLOY_DIR && microk8s kubectl apply -f k8s/ingress.yaml" ""
fi

# Clean up remote deployment directory
remote_exec "rm -rf $REMOTE_DEPLOY_DIR" "🧹 Cleaning up remote deployment files..."

# Show deployment status
print_status "📊 Deployment Status:"
remote_exec "microk8s kubectl get pods -n $NAMESPACE" ""
echo ""
remote_exec "microk8s kubectl get services -n $NAMESPACE" ""
echo ""
remote_exec "microk8s kubectl get ingress -n $NAMESPACE" ""

# Get access information
echo ""
print_status "🌍 Access Information:"

# Check if MetalLB is enabled for LoadBalancer services
if remote_exec "microk8s status | grep -q '^metallb: enabled'" "" >/dev/null 2>&1; then
    INGRESS_IP=$(remote_exec "microk8s kubectl get ingress azurephotoflow-ingress -n $NAMESPACE -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || echo ''" "")
    if [ -n "$INGRESS_IP" ]; then
        echo "External IP (via MetalLB): $INGRESS_IP"
        echo "Application will be available at: https://$INGRESS_IP (once DNS is configured)"
    else
        echo "LoadBalancer IP not yet assigned. Check again in a few minutes:"
        echo "  ssh $SSH_USER@$SSH_HOST 'microk8s kubectl get ingress azurephotoflow-ingress -n $NAMESPACE'"
    fi
else
    print_warning "MetalLB not enabled - using NodePort access"
    NODE_IP=$(remote_exec "microk8s kubectl get nodes -o jsonpath='{.items[0].status.addresses[?(@.type==\"InternalIP\")].address}'" "")
    INGRESS_HTTP_PORT=$(remote_exec "microk8s kubectl get svc -n ingress -o jsonpath='{.items[?(@.metadata.name==\"nginx-ingress-microk8s-controller\")].spec.ports[?(@.name==\"http\")].nodePort}' 2>/dev/null || echo '80'" "")
    INGRESS_HTTPS_PORT=$(remote_exec "microk8s kubectl get svc -n ingress -o jsonpath='{.items[?(@.metadata.name==\"nginx-ingress-microk8s-controller\")].spec.ports[?(@.name==\"https\")].nodePort}' 2>/dev/null || echo '443'" "")
    
    echo "Node IP: $NODE_IP"
    echo "HTTP Port: $INGRESS_HTTP_PORT"
    echo "HTTPS Port: $INGRESS_HTTPS_PORT"
    echo ""
    echo "Access URLs (configure your domain to point to $SSH_HOST):"
    echo "  HTTP:  http://$SSH_HOST:$INGRESS_HTTP_PORT"
    echo "  HTTPS: https://$SSH_HOST:$INGRESS_HTTPS_PORT"
fi

echo ""
print_success "Remote deployment completed successfully!"
echo ""
print_status "📋 Next steps:"
echo "1. Update DNS records to point your domain to the IP address above"
echo "2. Wait for SSL certificates to be issued"
echo "3. Test the application endpoints"
echo ""
print_status "🔧 Useful remote commands:"
echo "Monitor status: ssh $SSH_USER@$SSH_HOST && ./monitor-k8s.sh"
echo "Check logs: ssh $SSH_USER@$SSH_HOST 'microk8s kubectl logs -f deployment/backend-deployment -n $NAMESPACE'"
echo "Check certificates: ssh $SSH_USER@$SSH_HOST 'microk8s kubectl describe certificate -n $NAMESPACE'"
echo "Port forward for testing: ssh $SSH_USER@$SSH_HOST 'microk8s kubectl port-forward service/frontend-service 8080:80 -n $NAMESPACE'"