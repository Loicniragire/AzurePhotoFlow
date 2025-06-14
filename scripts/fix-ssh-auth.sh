#!/bin/bash

# Quick SSH Authentication Fix for AzurePhotoFlow
# This script performs common fixes for SSH key authentication issues

set -e

# SSH connection settings from your error
SSH_USER=${SSH_USER:-"loicn"}
SSH_HOST=${SSH_HOST:-"10.0.0.2"}
SSH_PORT=${SSH_PORT:-22}
SSH_KEY=${SSH_KEY:-"$HOME/.ssh/azurephotoflow-k8s"}

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

echo "🔧 Quick SSH Authentication Fix"
echo "Host: $SSH_HOST"
echo "User: $SSH_USER"
echo "Key: $SSH_KEY"
echo ""

# Step 1: Fix local key permissions
print_status "Step 1: Fixing local key permissions..."
chmod 600 "$SSH_KEY"
chmod 644 "$SSH_KEY.pub"
print_success "Local key permissions fixed"

# Step 2: Fix remote permissions and ensure key is properly added
print_status "Step 2: Fixing remote SSH setup..."
echo "This will require your password one more time..."

# Fix remote permissions
ssh -o PasswordAuthentication=yes -o PubkeyAuthentication=no -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'chmod 700 ~/.ssh 2>/dev/null || mkdir -p ~/.ssh && chmod 700 ~/.ssh'

# Recreate authorized_keys with our key
print_status "Step 3: Adding SSH key to authorized_keys..."
cat "$SSH_KEY.pub" | ssh -o PasswordAuthentication=yes -o PubkeyAuthentication=no -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'cat > ~/.ssh/authorized_keys.tmp && chmod 600 ~/.ssh/authorized_keys.tmp && mv ~/.ssh/authorized_keys.tmp ~/.ssh/authorized_keys'

print_success "SSH key added to authorized_keys"

# Step 4: Test authentication
print_status "Step 4: Testing SSH key authentication..."
sleep 1

if ssh -i "$SSH_KEY" -o PasswordAuthentication=no -o BatchMode=yes -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'echo "SSH key authentication successful!"' 2>/dev/null; then
    print_success "SSH key authentication is now working!"
    
    # Create environment file
    ENV_FILE="$HOME/.azurephotoflow-ssh.env"
    cat > "$ENV_FILE" << EOF
# AzurePhotoFlow Remote Deployment SSH Configuration
export SSH_HOST="$SSH_HOST"
export SSH_USER="$SSH_USER"
export SSH_PORT="$SSH_PORT"
export SSH_KEY="$SSH_KEY"
EOF
    
    print_success "Environment file created: $ENV_FILE"
    echo ""
    print_status "🎯 Next steps:"
    echo "1. Load environment: source ~/.azurephotoflow-ssh.env"
    echo "2. Test connection: ./scripts/ssh-helper.sh test"
    echo "3. Prepare cluster: ./scripts/prepare-microk8s-remote.sh"
    echo "4. Deploy app: ./scripts/deploy-k8s-remote.sh production latest"
    
else
    print_error "SSH authentication still not working"
    echo ""
    print_status "🔍 Let's check what's happening..."
    
    # Show verbose output for debugging
    echo "Testing with verbose output:"
    ssh -v -i "$SSH_KEY" -o PasswordAuthentication=no -o ConnectTimeout=5 -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'echo test' 2>&1 | grep -E "(debug1|Permission|Offering|Authentications|key)"
    
    echo ""
    print_status "🔧 Manual troubleshooting:"
    echo "1. Check authorized_keys content:"
    echo "   ssh loicn@10.0.0.2 'cat ~/.ssh/authorized_keys'"
    echo ""
    echo "2. Check SSH server logs:"
    echo "   ssh loicn@10.0.0.2 'sudo tail -10 /var/log/auth.log'"
    echo ""
    echo "3. Try connecting with password then switching:"
    echo "   ssh loicn@10.0.0.2"
    echo "   # Once connected, check: ls -la ~/.ssh/"
fi