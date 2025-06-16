#!/bin/bash

# MicroK8s Remote Installation Script for AzurePhotoFlow
# This script installs and configures MicroK8s on a remote server

set -e

# Color codes
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

print_status() { echo -e "${BLUE}$1${NC}"; }
print_success() { echo -e "${GREEN}✅ $1${NC}"; }
print_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }
print_error() { echo -e "${RED}❌ $1${NC}"; }
print_info() { echo -e "${CYAN}ℹ️  $1${NC}"; }

# Load SSH environment
SSH_ENV_FILE="$HOME/.ssh_env"
if [ -f "$SSH_ENV_FILE" ]; then
    source "$SSH_ENV_FILE"
fi

# Default values
SSH_HOST=${SSH_HOST:-"10.0.0.2"}
SSH_USER=${SSH_USER:-"loicn"}
SSH_PORT=${SSH_PORT:-"22"}

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
        --help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  -h, --host HOST    Remote server hostname/IP (default: 10.0.0.2)"
            echo "  -u, --user USER    SSH username (default: loicn)"
            echo "  -p, --port PORT    SSH port (default: 22)"
            echo "  --help            Show this help message"
            echo ""
            echo "Environment variables:"
            echo "  SSH_HOST, SSH_USER, SSH_PORT"
            echo ""
            echo "Examples:"
            echo "  $0 -h 192.168.1.100 -u ubuntu"
            echo "  SSH_HOST=10.0.0.2 SSH_USER=loicn $0"
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

print_status "🚀 Installing MicroK8s on $SSH_USER@$SSH_HOST:$SSH_PORT"
echo ""

# Function to execute remote commands
remote_exec() {
    local command="$1"
    local description="$2"
    local allow_failure="${3:-false}"
    
    if [ -n "$description" ]; then
        print_status "$description"
    fi
    
    if ssh -o ConnectTimeout=10 -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null \
       -o LogLevel=ERROR -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" "$command"; then
        return 0
    else
        if [ "$allow_failure" = "true" ]; then
            return 1
        else
            print_error "Command failed: $command"
            exit 1
        fi
    fi
}

# Test SSH connectivity
print_status "🔍 Testing SSH connectivity..."
if remote_exec "echo 'SSH connection successful'" "" "true"; then
    print_success "SSH connection established"
else
    print_error "Cannot connect to $SSH_USER@$SSH_HOST:$SSH_PORT"
    print_info "Please check:"
    print_info "  - Server is running and accessible"
    print_info "  - SSH credentials are correct"
    print_info "  - SSH service is running on the server"
    exit 1
fi

# Check if MicroK8s is already installed
print_status "🔍 Checking if MicroK8s is already installed..."
if remote_exec "command -v microk8s >/dev/null 2>&1 && echo 'installed'" "" "true" | grep -q "installed"; then
    print_warning "MicroK8s is already installed"
    
    # Check version
    VERSION=$(remote_exec "microk8s version --short" "" "true" 2>/dev/null || echo "unknown")
    print_info "Current version: $VERSION"
    
    echo ""
    read -p "Do you want to continue with configuration? (y/N): " continue_config
    if [[ ! "$continue_config" =~ ^[Yy]$ ]]; then
        print_info "Installation cancelled"
        exit 0
    fi
    
    SKIP_INSTALL=true
else
    print_info "MicroK8s is not installed - proceeding with installation"
    SKIP_INSTALL=false
fi

# Update system packages
if [ "$SKIP_INSTALL" = "false" ]; then
    print_status "📦 Updating system packages..."
    remote_exec "sudo apt update" "Updating package lists"
fi

# Install snapd if not present
if [ "$SKIP_INSTALL" = "false" ]; then
    print_status "📦 Ensuring snapd is installed..."
    remote_exec "sudo apt install -y snapd" "Installing snapd"
fi

# Install MicroK8s
if [ "$SKIP_INSTALL" = "false" ]; then
    print_status "⬇️  Installing MicroK8s (this may take a few minutes)..."
    remote_exec "sudo snap install microk8s --classic" "Installing MicroK8s via snap"
    print_success "MicroK8s installation completed"
fi

# Add user to microk8s group
print_status "👤 Configuring user permissions..."
remote_exec "sudo usermod -a -G microk8s \$USER" "Adding user to microk8s group"

# Set up kubectl alias (optional)
print_status "🔗 Setting up kubectl alias..."
remote_exec "echo 'alias kubectl=\"microk8s kubectl\"' >> ~/.bashrc" "Adding kubectl alias" "true"

# Wait for MicroK8s to be ready
print_status "⏳ Waiting for MicroK8s to be ready (this may take a few minutes)..."
if remote_exec "timeout 300 microk8s status --wait-ready" "Waiting for cluster startup" "true"; then
    print_success "MicroK8s is ready"
else
    print_warning "MicroK8s startup is taking longer than expected"
    print_info "You may need to wait a bit longer or check the status manually"
fi

# Enable essential addons
print_status "🔌 Enabling essential addons..."
remote_exec "microk8s enable dns" "Enabling DNS addon"
remote_exec "microk8s enable storage" "Enabling storage addon"
remote_exec "microk8s enable ingress" "Enabling ingress addon"

# Enable recommended addons
print_status "🔌 Enabling recommended addons..."
remote_exec "microk8s enable cert-manager" "Enabling cert-manager addon" "true"
remote_exec "microk8s enable metrics-server" "Enabling metrics-server addon" "true"

# Show cluster status
print_status "📊 Checking cluster status..."
remote_exec "microk8s status" "Current cluster status:"

# Test basic functionality
print_status "🧪 Testing basic functionality..."
remote_exec "microk8s kubectl get nodes" "Cluster nodes:"
remote_exec "microk8s kubectl get pods --all-namespaces" "System pods:"

# Show storage classes
print_status "💾 Storage configuration:"
remote_exec "microk8s kubectl get storageclass" "Available storage classes:"

print_success "🎉 MicroK8s installation and configuration completed successfully!"
echo ""
print_info "Next steps:"
print_info "1. Run cluster configuration check:"
print_info "   python3 scripts/check-cluster-config.py -h $SSH_HOST -u $SSH_USER"
print_info ""
print_info "2. Deploy AzurePhotoFlow:"
print_info "   ./scripts/smart-deploy.sh"
print_info ""
print_info "3. Or run the full CI/CD pipeline"
print_info ""
print_warning "Note: You may need to log out and back in to $SSH_HOST for group changes to take effect"
print_info "Or use: ssh $SSH_USER@$SSH_HOST 'newgrp microk8s'"