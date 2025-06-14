#!/bin/bash

# AzurePhotoFlow Remote Deployment Master Script
# This script guides you through the entire deployment process step by step

set -e

# Color codes
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

print_header() { echo -e "${BOLD}${BLUE}========================================${NC}"; echo -e "${BOLD}${BLUE}$1${NC}"; echo -e "${BOLD}${BLUE}========================================${NC}"; }
print_step() { echo -e "${CYAN}📋 $1${NC}"; }
print_success() { echo -e "${GREEN}✅ $1${NC}"; }
print_error() { echo -e "${RED}❌ $1${NC}"; }
print_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }
print_info() { echo -e "${BLUE}ℹ️  $1${NC}"; }

# Function to wait for user confirmation
wait_for_user() {
    echo ""
    read -p "Press ENTER to continue or Ctrl+C to exit..."
    echo ""
}

# Function to ask yes/no question
ask_yes_no() {
    local question="$1"
    local default="${2:-n}"
    
    if [ "$default" = "y" ]; then
        prompt="$question (Y/n): "
    else
        prompt="$question (y/N): "
    fi
    
    read -p "$prompt" answer
    
    if [ "$default" = "y" ]; then
        [[ -z "$answer" || "$answer" =~ ^[Yy]$ ]]
    else
        [[ "$answer" =~ ^[Yy]$ ]]
    fi
}

# Function to get user input
get_input() {
    local prompt="$1"
    local default="$2"
    local var_name="$3"
    
    if [ -n "$default" ]; then
        read -p "$prompt (default: $default): " input
        input="${input:-$default}"
    else
        read -p "$prompt: " input
    fi
    
    eval "$var_name='$input'"
}

# Initialize variables
SSH_HOST=""
SSH_USER=""
SSH_PORT="22"
DOMAIN=""
GOOGLE_CLIENT_ID=""
IMAGE_TAG="latest"

echo ""
print_header "🚀 AzurePhotoFlow Remote Deployment"
echo ""
print_info "This script will guide you through deploying AzurePhotoFlow to your remote MicroK8s cluster."
echo ""
echo "What this script will do:"
echo "  1. ✅ Setup/verify SSH connection"
echo "  2. ✅ Prepare your MicroK8s cluster"
echo "  3. ✅ Configure application secrets"
echo "  4. ✅ Update domain configuration"
echo "  5. ✅ Deploy the application"
echo "  6. ✅ Verify deployment"
echo ""
wait_for_user

# Step 1: Collect connection details
print_header "📡 Step 1: SSH Connection Setup"
echo ""

# Load existing environment if available
if [ -f "$HOME/.azurephotoflow-ssh.env" ]; then
    print_info "Found existing SSH configuration. Loading..."
    source "$HOME/.azurephotoflow-ssh.env"
    echo "  Current settings:"
    echo "    Host: ${SSH_HOST:-'(not set)'}"
    echo "    User: ${SSH_USER:-'(not set)'}"
    echo "    Port: ${SSH_PORT:-22}"
    echo ""
    
    if ask_yes_no "Use existing SSH configuration?" "y"; then
        print_success "Using existing SSH configuration"
    else
        SSH_HOST=""
        SSH_USER=""
    fi
fi

# Get SSH details if not already set
if [ -z "$SSH_HOST" ] || [ -z "$SSH_USER" ]; then
    echo "Enter your remote server details:"
    get_input "Server IP address or hostname" "" "SSH_HOST"
    get_input "SSH username" "" "SSH_USER"
    get_input "SSH port" "22" "SSH_PORT"
    
    # Test and setup SSH
    print_step "Testing SSH connection..."
    
    if ! ssh -o BatchMode=yes -o ConnectTimeout=5 -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'echo "SSH works"' >/dev/null 2>&1; then
        print_warning "SSH key authentication not working. Setting it up..."
        
        if [ ! -f "$HOME/.ssh/azurephotoflow-k8s" ]; then
            print_step "Generating SSH key..."
            ssh-keygen -t rsa -b 4096 -f "$HOME/.ssh/azurephotoflow-k8s" -N ""
        fi
        
        print_step "Setting up SSH key authentication..."
        echo "This will require your password for the remote server."
        
        # Run the fix script
        SSH_HOST="$SSH_HOST" SSH_USER="$SSH_USER" SSH_PORT="$SSH_PORT" ./scripts/fix-ssh-auth.sh
        
        # Load the created environment
        source "$HOME/.azurephotoflow-ssh.env"
    fi
else
    # Test existing connection
    print_step "Testing existing SSH connection..."
    if ssh -o BatchMode=yes -o ConnectTimeout=5 -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'echo "SSH works"' >/dev/null 2>&1; then
        print_success "SSH connection verified"
    else
        print_error "SSH connection failed. Please check your configuration."
        exit 1
    fi
fi

echo ""
print_success "SSH connection ready!"
wait_for_user

# Step 2: Prepare MicroK8s cluster
print_header "🔧 Step 2: MicroK8s Cluster Preparation"
echo ""

print_step "Checking and preparing your MicroK8s cluster..."
if ./scripts/prepare-microk8s-remote.sh; then
    print_success "MicroK8s cluster is ready!"
else
    print_error "MicroK8s cluster preparation failed"
    echo ""
    print_info "Common solutions:"
    echo "  1. Enable required addons:"
    echo "     ssh $SSH_USER@$SSH_HOST 'microk8s enable dns storage'"
    echo "  2. Enable recommended addons:"
    echo "     ssh $SSH_USER@$SSH_HOST 'microk8s enable ingress cert-manager'"
    echo ""
    if ask_yes_no "Try to enable addons automatically?" "y"; then
        print_step "Enabling MicroK8s addons..."
        ssh "$SSH_USER@$SSH_HOST" 'microk8s enable dns storage ingress cert-manager metrics-server'
        print_success "Addons enabled. Re-checking cluster..."
        
        if ./scripts/prepare-microk8s-remote.sh; then
            print_success "MicroK8s cluster is now ready!"
        else
            print_error "Still having issues. Please check manually and re-run this script."
            exit 1
        fi
    else
        echo "Please fix the issues and re-run this script."
        exit 1
    fi
fi

wait_for_user

# Step 3: Configure application secrets
print_header "🔐 Step 3: Application Configuration"
echo ""

print_step "Setting up application secrets..."
echo ""
echo "You'll need to provide:"
echo "  - Google OAuth Client ID (for user authentication)"
echo "  - Your domain name (for the application)"
echo "  - GitHub Container Registry credentials (for Docker images)"
echo ""

get_input "Enter your domain name (e.g., myapp.example.com)" "" "DOMAIN"
get_input "Enter Google OAuth Client ID" "" "GOOGLE_CLIENT_ID"

# Update configmap with domain
print_step "Updating application configuration..."
sed -i.bak "s/your-domain.com/$DOMAIN/g" k8s/configmap.yaml
sed -i.bak "s/your-domain.com/$DOMAIN/g" k8s/ingress-microk8s.yaml
print_success "Configuration files updated with your domain"

# Setup secrets
print_step "Setting up secrets on remote cluster..."
echo ""
echo "For GitHub Container Registry, you can use:"
echo "  - Username: your-github-username"
echo "  - Password/Token: GitHub Personal Access Token with package:read permission"
echo ""

if ./scripts/setup-secrets-remote.sh; then
    print_success "Secrets configured successfully!"
else
    print_error "Secret setup failed"
    exit 1
fi

wait_for_user

# Step 4: Deploy application
print_header "🚀 Step 4: Application Deployment"
echo ""

get_input "Enter image tag to deploy" "latest" "IMAGE_TAG"

print_step "Deploying AzurePhotoFlow to your cluster..."
echo "This will deploy:"
echo "  - Storage components (MinIO, Qdrant)"
echo "  - Application components (Backend, Frontend)"
echo "  - Ingress configuration"
echo ""

if ./scripts/deploy-k8s-remote.sh production "$IMAGE_TAG"; then
    print_success "Application deployed successfully!"
else
    print_error "Deployment failed"
    exit 1
fi

wait_for_user

# Step 5: Verify deployment
print_header "✅ Step 5: Deployment Verification"
echo ""

print_step "Running health checks..."
if ./scripts/monitor-k8s-remote.sh health; then
    print_success "All health checks passed!"
else
    print_warning "Some health checks failed. Let's investigate..."
fi

echo ""
print_step "Deployment status:"
./scripts/monitor-k8s-remote.sh status

wait_for_user

# Step 6: Final instructions
print_header "🎉 Step 6: Deployment Complete!"
echo ""

print_success "AzurePhotoFlow has been deployed to your MicroK8s cluster!"
echo ""

# Get access information
print_step "Getting access information..."
INGRESS_OUTPUT=$(ssh "$SSH_USER@$SSH_HOST" 'microk8s kubectl get ingress azurephotoflow-ingress -n azurephotoflow 2>/dev/null' || echo "")

if echo "$INGRESS_OUTPUT" | grep -q "azurephotoflow-ingress"; then
    EXTERNAL_IP=$(echo "$INGRESS_OUTPUT" | tail -1 | awk '{print $4}')
    if [ "$EXTERNAL_IP" != "<pending>" ] && [ -n "$EXTERNAL_IP" ]; then
        print_info "External IP: $EXTERNAL_IP"
    else
        print_info "External IP: Using NodePort access"
        EXTERNAL_IP="$SSH_HOST"
    fi
else
    EXTERNAL_IP="$SSH_HOST"
fi

echo ""
print_header "📋 Next Steps"
echo ""

echo "1. 🌐 Configure DNS:"
echo "   Point your domain to the server IP:"
echo "   $DOMAIN -> $EXTERNAL_IP"
echo ""

echo "2. 🔒 SSL Certificates:"
echo "   Certificates will be automatically issued by Let's Encrypt"
echo "   Check status: ssh $SSH_USER@$SSH_HOST 'microk8s kubectl get certificates -n azurephotoflow'"
echo ""

echo "3. 🧪 Test your application:"
echo "   Frontend: https://$DOMAIN"
echo "   API: https://$DOMAIN/api/health"
echo ""

echo "4. 📊 Monitor your deployment:"
echo "   ./scripts/monitor-k8s-remote.sh"
echo "   ./scripts/monitor-k8s-remote.sh logs backend-deployment-xxx"
echo ""

echo "5. 🔧 Useful commands:"
echo "   # SSH to server"
echo "   ssh $SSH_USER@$SSH_HOST"
echo "   "
echo "   # Check pod status"
echo "   microk8s kubectl get pods -n azurephotoflow"
echo "   "
echo "   # Port forward for testing (run on your local machine)"
echo "   ssh -L 8080:localhost:80 $SSH_USER@$SSH_HOST 'microk8s kubectl port-forward service/frontend-service 80:80 -n azurephotoflow'"
echo "   # Then visit http://localhost:8080"
echo ""

# Save deployment info
cat > ~/.azurephotoflow-deployment.info << EOF
# AzurePhotoFlow Deployment Information
DEPLOYMENT_DATE=$(date)
SSH_HOST=$SSH_HOST
SSH_USER=$SSH_USER
DOMAIN=$DOMAIN
IMAGE_TAG=$IMAGE_TAG
EXTERNAL_IP=$EXTERNAL_IP

# Quick commands
# Monitor: ./scripts/monitor-k8s-remote.sh
# Logs: ./scripts/monitor-k8s-remote.sh logs <pod-name>
# SSH: ssh $SSH_USER@$SSH_HOST
# Redeploy: ./deploy-remote.sh
EOF

print_success "Deployment information saved to ~/.azurephotoflow-deployment.info"
echo ""

if ask_yes_no "Would you like to open the monitoring dashboard?" "y"; then
    ./scripts/monitor-k8s-remote.sh
fi

echo ""
print_success "🎉 Deployment complete! Your AzurePhotoFlow application should be accessible at https://$DOMAIN"
echo ""
print_info "If you need help, check the logs or re-run this script: ./deploy-remote.sh"