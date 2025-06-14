#!/bin/bash

# Remote secrets setup script for AzurePhotoFlow
# This script sets up secrets on a remote MicroK8s cluster via SSH

set -e

# SSH connection settings
SSH_USER=${SSH_USER:-""}
SSH_HOST=${SSH_HOST:-""}
SSH_PORT=${SSH_PORT:-22}
SSH_KEY=${SSH_KEY:-""}
SSH_OPTIONS="-o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR -o ControlMaster=auto -o ControlPath=~/.ssh/control-%r@%h:%p -o ControlPersist=10m"
NAMESPACE="azurephotoflow"

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
    echo "Usage: $0 [OPTIONS]"
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
    echo "  $0 -h 192.168.1.100 -u ubuntu"
    echo "  SSH_HOST=k8s-server.local SSH_USER=admin $0"
}

# Parse command line arguments
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
    $SSH_CMD "$command"
}

echo "🔐 Setting up secrets for AzurePhotoFlow on remote MicroK8s cluster"
echo "Remote Server: $SSH_USER@$SSH_HOST:$SSH_PORT"
echo "Namespace: $NAMESPACE"
echo ""

# Check SSH connectivity
print_status "🔌 Testing SSH connection..."
if ! remote_exec "echo 'SSH connection successful'" >/dev/null 2>&1; then
    print_error "Cannot establish SSH connection to $SSH_USER@$SSH_HOST"
    exit 1
fi
print_success "SSH connection established"

# Check if MicroK8s is ready
print_status "🔍 Checking remote MicroK8s status..."
if ! remote_exec "microk8s status --wait-ready --timeout=30 >/dev/null 2>&1"; then
    print_error "MicroK8s is not ready on remote server"
    exit 1
fi
print_success "Remote MicroK8s cluster is ready"

# Create namespace if it doesn't exist
print_status "📁 Ensuring namespace exists..."
remote_exec "microk8s kubectl create namespace $NAMESPACE 2>/dev/null || echo 'Namespace already exists'"

# Check if secrets already exist
print_status "🔍 Checking existing secrets..."
if remote_exec "microk8s kubectl get secret azurephotoflow-secrets -n $NAMESPACE >/dev/null 2>&1"; then
    print_warning "Application secrets already exist"
    read -p "Do you want to update them? (y/N): " update_secrets
    if [[ ! $update_secrets =~ ^[Yy]$ ]]; then
        print_status "Keeping existing secrets"
        UPDATE_SECRETS=false
    else
        UPDATE_SECRETS=true
    fi
else
    UPDATE_SECRETS=true
fi

if remote_exec "microk8s kubectl get secret registry-secret -n $NAMESPACE >/dev/null 2>&1"; then
    print_warning "Registry secrets already exist"
    read -p "Do you want to update them? (y/N): " update_registry
    if [[ ! $update_registry =~ ^[Yy]$ ]]; then
        print_status "Keeping existing registry secrets"
        UPDATE_REGISTRY=false
    else
        UPDATE_REGISTRY=true
    fi
else
    UPDATE_REGISTRY=true
fi

# Collect application secrets
if [ "$UPDATE_SECRETS" = true ]; then
    echo ""
    print_status "🔑 Collecting application secrets..."
    echo ""
    
    # Google OAuth Client ID
    read -p "Enter Google OAuth Client ID: " GOOGLE_CLIENT_ID
    if [ -z "$GOOGLE_CLIENT_ID" ]; then
        print_error "Google OAuth Client ID is required"
        exit 1
    fi
    
    # JWT Secret Key
    read -p "Enter JWT Secret Key (leave empty to generate): " JWT_SECRET
    if [ -z "$JWT_SECRET" ]; then
        JWT_SECRET=$(openssl rand -base64 32)
        print_success "Generated JWT Secret Key"
    fi
    
    # MinIO credentials (can use defaults for development)
    read -p "Enter MinIO Access Key (default: minioadmin): " MINIO_ACCESS_KEY
    MINIO_ACCESS_KEY=${MINIO_ACCESS_KEY:-minioadmin}
    
    read -p "Enter MinIO Secret Key (default: minioadmin): " MINIO_SECRET_KEY
    MINIO_SECRET_KEY=${MINIO_SECRET_KEY:-minioadmin}
    
    # Create application secrets on remote server
    print_status "📤 Creating application secrets on remote server..."
    
    # Delete existing secret if updating
    if [ "$UPDATE_SECRETS" = true ]; then
        remote_exec "microk8s kubectl delete secret azurephotoflow-secrets -n $NAMESPACE 2>/dev/null || true"
    fi
    
    # Create new secret
    remote_exec "microk8s kubectl create secret generic azurephotoflow-secrets \
        --from-literal=VITE_GOOGLE_CLIENT_ID='$GOOGLE_CLIENT_ID' \
        --from-literal=JWT_SECRET_KEY='$JWT_SECRET' \
        --from-literal=MINIO_ACCESS_KEY='$MINIO_ACCESS_KEY' \
        --from-literal=MINIO_SECRET_KEY='$MINIO_SECRET_KEY' \
        --namespace=$NAMESPACE"
    
    print_success "Application secrets created"
fi

# Collect registry secrets
if [ "$UPDATE_REGISTRY" = true ]; then
    echo ""
    print_status "🐳 Collecting Docker registry secrets..."
    echo ""
    
    read -p "Enter Docker registry server (default: ghcr.io): " REGISTRY_SERVER
    REGISTRY_SERVER=${REGISTRY_SERVER:-ghcr.io}
    
    read -p "Enter Docker registry username: " REGISTRY_USERNAME
    if [ -z "$REGISTRY_USERNAME" ]; then
        print_error "Registry username is required"
        exit 1
    fi
    
    read -s -p "Enter Docker registry password/token: " REGISTRY_PASSWORD
    echo ""
    if [ -z "$REGISTRY_PASSWORD" ]; then
        print_error "Registry password is required"
        exit 1
    fi
    
    # Create registry secrets on remote server
    print_status "📤 Creating registry secrets on remote server..."
    
    # Delete existing secret if updating
    if [ "$UPDATE_REGISTRY" = true ]; then
        remote_exec "microk8s kubectl delete secret registry-secret -n $NAMESPACE 2>/dev/null || true"
    fi
    
    # Create new secret
    remote_exec "microk8s kubectl create secret docker-registry registry-secret \
        --docker-server='$REGISTRY_SERVER' \
        --docker-username='$REGISTRY_USERNAME' \
        --docker-password='$REGISTRY_PASSWORD' \
        --namespace=$NAMESPACE"
    
    print_success "Registry secrets created"
fi

# Verify secrets were created
echo ""
print_status "🔍 Verifying secrets on remote server..."
remote_exec "microk8s kubectl get secrets -n $NAMESPACE"

echo ""
print_success "Remote secrets setup completed!"
echo ""
print_status "📋 Next steps:"
echo "1. Update domain configuration in k8s/configmap.yaml and k8s/ingress-microk8s.yaml"
echo "2. Deploy application: ./scripts/deploy-k8s-remote.sh production latest"
echo ""
print_status "🔧 Useful commands:"
echo "View secrets: ssh $SSH_USER@$SSH_HOST 'microk8s kubectl get secrets -n $NAMESPACE'"
echo "Describe secret: ssh $SSH_USER@$SSH_HOST 'microk8s kubectl describe secret azurephotoflow-secrets -n $NAMESPACE'"