#!/bin/bash

# SSH Key Setup Script for AzurePhotoFlow Remote Deployment
# This script sets up passwordless SSH access to your remote MicroK8s server

set -e

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

# Default values
SSH_KEY_NAME="azurephotoflow-k8s"
SSH_KEY_PATH="$HOME/.ssh/$SSH_KEY_NAME"
SSH_HOST=""
SSH_USER=""
SSH_PORT=22

# Function to show usage
show_usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "This script sets up passwordless SSH access to your remote MicroK8s server."
    echo ""
    echo "Options:"
    echo "  -h, --host HOST       Remote server hostname/IP (required)"
    echo "  -u, --user USER       SSH username (required)"
    echo "  -p, --port PORT       SSH port (default: 22)"
    echo "  -k, --key-name NAME   SSH key name (default: azurephotoflow-k8s)"
    echo "  --help                Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 -h 192.168.1.100 -u ubuntu"
    echo "  $0 -h k8s-server.local -u admin -k my-k8s-key"
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
        -k|--key-name)
            SSH_KEY_NAME="$2"
            SSH_KEY_PATH="$HOME/.ssh/$SSH_KEY_NAME"
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

# Validate required parameters
if [ -z "$SSH_HOST" ] || [ -z "$SSH_USER" ]; then
    print_error "SSH host and user are required"
    show_usage
    exit 1
fi

echo "🔑 Setting up SSH key authentication for AzurePhotoFlow deployment"
echo "Remote Server: $SSH_USER@$SSH_HOST:$SSH_PORT"
echo "SSH Key: $SSH_KEY_PATH"
echo ""

# Create .ssh directory if it doesn't exist
if [ ! -d "$HOME/.ssh" ]; then
    print_status "📁 Creating ~/.ssh directory..."
    mkdir -p "$HOME/.ssh"
    chmod 700 "$HOME/.ssh"
    print_success "Created ~/.ssh directory"
fi

# Check if SSH key already exists
if [ -f "$SSH_KEY_PATH" ]; then
    print_warning "SSH key already exists at $SSH_KEY_PATH"
    read -p "Do you want to use the existing key? (Y/n): " use_existing
    
    if [[ $use_existing =~ ^[Nn]$ ]]; then
        read -p "Do you want to overwrite the existing key? (y/N): " overwrite
        if [[ ! $overwrite =~ ^[Yy]$ ]]; then
            print_status "Cancelled. Use a different key name with -k option."
            exit 0
        fi
        print_warning "Removing existing key..."
        rm -f "$SSH_KEY_PATH" "$SSH_KEY_PATH.pub"
    else
        EXISTING_KEY=true
    fi
fi

# Generate SSH key if it doesn't exist
if [ ! -f "$SSH_KEY_PATH" ]; then
    print_status "🔐 Generating SSH key pair..."
    
    # Get email for key comment
    read -p "Enter your email address for the SSH key comment (optional): " email
    comment=""
    if [ -n "$email" ]; then
        comment="-C $email"
    fi
    
    # Generate key
    ssh-keygen -t rsa -b 4096 -f "$SSH_KEY_PATH" $comment -N ""
    
    print_success "SSH key pair generated"
    echo "  Private key: $SSH_KEY_PATH"
    echo "  Public key: $SSH_KEY_PATH.pub"
fi

# Test current SSH connectivity
print_status "🔌 Testing SSH connectivity..."
SSH_TEST_CMD="ssh -o BatchMode=yes -o ConnectTimeout=5 -p $SSH_PORT $SSH_USER@$SSH_HOST 'echo success'"

if $SSH_TEST_CMD >/dev/null 2>&1; then
    print_success "SSH connection already works (possibly using existing key or password)"
    HAS_ACCESS=true
else
    print_warning "SSH connection requires authentication setup"
    HAS_ACCESS=false
fi

# Copy public key to remote server
if [ "$HAS_ACCESS" = false ] || [ "$EXISTING_KEY" != true ]; then
    print_status "📤 Copying public key to remote server..."
    echo ""
    echo "This will require your password for the remote server."
    
    # Use ssh-copy-id if available, otherwise manual method
    if command -v ssh-copy-id >/dev/null 2>&1; then
        if ssh-copy-id -i "$SSH_KEY_PATH.pub" -p "$SSH_PORT" "$SSH_USER@$SSH_HOST"; then
            print_success "Public key copied successfully"
        else
            print_error "Failed to copy public key"
            exit 1
        fi
    else
        # Manual method
        print_status "ssh-copy-id not available, using manual method..."
        cat "$SSH_KEY_PATH.pub" | ssh -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'mkdir -p ~/.ssh && chmod 700 ~/.ssh && cat >> ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys'
        
        if [ $? -eq 0 ]; then
            print_success "Public key copied manually"
        else
            print_error "Failed to copy public key manually"
            exit 1
        fi
    fi
fi

# Test key-based authentication
print_status "🔑 Testing key-based authentication..."
SSH_KEY_TEST_CMD="ssh -i $SSH_KEY_PATH -o BatchMode=yes -o ConnectTimeout=5 -p $SSH_PORT $SSH_USER@$SSH_HOST 'echo success'"

if $SSH_KEY_TEST_CMD >/dev/null 2>&1; then
    print_success "Key-based authentication is working!"
else
    print_error "Key-based authentication failed"
    echo ""
    print_warning "Running automated diagnostics and fixes..."
    
    # Try to fix common issues automatically
    echo "Attempting to fix permissions and re-add key..."
    
    # Fix local key permissions
    chmod 600 "$SSH_KEY_PATH"
    
    # Try to fix remote permissions and re-add key
    if ssh -o PasswordAuthentication=yes -o PubkeyAuthentication=no -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'chmod 700 ~/.ssh && chmod 600 ~/.ssh/authorized_keys 2>/dev/null || true' 2>/dev/null; then
        print_status "Fixed remote permissions"
        
        # Re-add our key to make sure it's properly formatted
        our_key=$(cat "$SSH_KEY_PATH.pub")
        if ssh -o PasswordAuthentication=yes -o PubkeyAuthentication=no -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" "grep -q \"$(echo "$our_key" | cut -d' ' -f2)\" ~/.ssh/authorized_keys || echo '$our_key' >> ~/.ssh/authorized_keys" 2>/dev/null; then
            print_status "Ensured key is in authorized_keys"
        fi
        
        # Test again
        sleep 1
        if $SSH_KEY_TEST_CMD >/dev/null 2>&1; then
            print_success "Key-based authentication is now working!"
        else
            print_error "Authentication still failing after fixes"
            echo ""
            print_status "🔧 Run detailed diagnostics:"
            echo "   ./scripts/debug-ssh.sh"
            echo ""
            echo "Manual troubleshooting steps:"
            echo "1. Check if the public key was added correctly:"
            echo "   ssh -p $SSH_PORT $SSH_USER@$SSH_HOST 'cat ~/.ssh/authorized_keys'"
            echo "2. Check SSH server configuration on remote server"
            echo "3. Verify file permissions on remote server:"
            echo "   ssh -p $SSH_PORT $SSH_USER@$SSH_HOST 'ls -la ~/.ssh/'"
            echo "4. Check SSH server logs:"
            echo "   ssh -p $SSH_PORT $SSH_USER@$SSH_HOST 'sudo tail -10 /var/log/auth.log'"
            exit 1
        fi
    else
        print_error "Cannot connect with password authentication"
        echo ""
        print_status "🔧 Run detailed diagnostics:"
        echo "   ./scripts/debug-ssh.sh"
        exit 1
    fi
fi

# Set up SSH config for convenience
SSH_CONFIG_FILE="$HOME/.ssh/config"
SSH_CONFIG_ENTRY="azurephotoflow-k8s"

print_status "⚙️ Setting up SSH config entry..."

# Check if config entry already exists
if [ -f "$SSH_CONFIG_FILE" ] && grep -q "Host $SSH_CONFIG_ENTRY" "$SSH_CONFIG_FILE"; then
    print_warning "SSH config entry already exists"
    read -p "Do you want to update it? (y/N): " update_config
    
    if [[ $update_config =~ ^[Yy]$ ]]; then
        # Remove existing entry
        sed -i.bak "/^Host $SSH_CONFIG_ENTRY$/,/^$/d" "$SSH_CONFIG_FILE"
        print_status "Removed existing SSH config entry"
    else
        print_status "Keeping existing SSH config entry"
        SKIP_CONFIG=true
    fi
fi

if [ "$SKIP_CONFIG" != true ]; then
    # Create or append to SSH config
    cat >> "$SSH_CONFIG_FILE" << EOF

Host $SSH_CONFIG_ENTRY
    HostName $SSH_HOST
    User $SSH_USER
    Port $SSH_PORT
    IdentityFile $SSH_KEY_PATH
    ControlMaster auto
    ControlPath ~/.ssh/control-%r@%h:%p
    ControlPersist 10m
    StrictHostKeyChecking no
    UserKnownHostsFile /dev/null
    LogLevel ERROR

EOF

    print_success "SSH config entry created"
    echo "You can now connect using: ssh $SSH_CONFIG_ENTRY"
fi

# Test the SSH config entry
if [ "$SKIP_CONFIG" != true ]; then
    print_status "🧪 Testing SSH config entry..."
    if ssh -o BatchMode=yes -o ConnectTimeout=5 "$SSH_CONFIG_ENTRY" 'echo success' >/dev/null 2>&1; then
        print_success "SSH config entry is working!"
    else
        print_warning "SSH config entry test failed, but direct key access works"
    fi
fi

# Create environment variables file
ENV_FILE="$HOME/.azurephotoflow-ssh.env"
print_status "📝 Creating environment variables file..."

cat > "$ENV_FILE" << EOF
# AzurePhotoFlow Remote Deployment SSH Configuration
# Source this file to set environment variables for remote deployment scripts

export SSH_HOST="$SSH_HOST"
export SSH_USER="$SSH_USER"
export SSH_PORT="$SSH_PORT"
export SSH_KEY="$SSH_KEY_PATH"

# SSH config alias (if you prefer using the alias)
export SSH_ALIAS="$SSH_CONFIG_ENTRY"
EOF

print_success "Environment file created: $ENV_FILE"

echo ""
print_success "SSH key setup completed successfully!"
echo ""
print_status "📋 Next steps:"
echo ""
echo "1. Source the environment file to set SSH variables:"
echo "   source $ENV_FILE"
echo ""
echo "2. Or export them manually:"
echo "   export SSH_HOST=\"$SSH_HOST\""
echo "   export SSH_USER=\"$SSH_USER\""
echo "   export SSH_KEY=\"$SSH_KEY_PATH\""
echo ""
echo "3. Run the cluster preparation script:"
echo "   ./scripts/prepare-microk8s-remote.sh"
echo ""
echo "4. Continue with the deployment:"
echo "   ./scripts/setup-secrets-remote.sh"
echo "   ./scripts/deploy-k8s-remote.sh production latest"
echo ""
print_status "🎯 Alternative usage with SSH config alias:"
echo "You can also modify the scripts to use the SSH config entry:"
echo "   ssh $SSH_CONFIG_ENTRY 'microk8s status'"
echo ""
print_status "🔧 Environment file usage:"
echo "Add this to your ~/.bashrc or ~/.zshrc for permanent setup:"
echo "   source $ENV_FILE"

# Test remote MicroK8s access
echo ""
read -p "Do you want to test MicroK8s access on the remote server? (Y/n): " test_microk8s

if [[ ! $test_microk8s =~ ^[Nn]$ ]]; then
    print_status "🔍 Testing MicroK8s access..."
    
    SSH_KEY_ARG=""
    if [ -n "$SSH_KEY_PATH" ]; then
        SSH_KEY_ARG="-i $SSH_KEY_PATH"
    fi
    
    if ssh $SSH_KEY_ARG -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'microk8s status --wait-ready --timeout=10 >/dev/null 2>&1 && echo "MicroK8s is ready" || echo "MicroK8s is not ready"' | grep -q "ready"; then
        print_success "MicroK8s is accessible and ready on remote server!"
    else
        print_warning "MicroK8s is not ready on remote server"
        echo "You may need to start it: ssh $SSH_CONFIG_ENTRY 'microk8s start'"
    fi
fi

echo ""
print_success "Setup complete! You can now use passwordless SSH for deployment."