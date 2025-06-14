#!/bin/bash

# SSH Debug Script for AzurePhotoFlow Remote Deployment
# This script helps diagnose SSH authentication issues

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

# SSH connection settings
SSH_USER=${SSH_USER:-"loicn"}
SSH_HOST=${SSH_HOST:-"10.0.0.2"}
SSH_PORT=${SSH_PORT:-22}
SSH_KEY=${SSH_KEY:-"$HOME/.ssh/azurephotoflow-k8s"}

echo "🔍 SSH Authentication Diagnostic Tool"
echo "Host: $SSH_HOST"
echo "User: $SSH_USER"
echo "Port: $SSH_PORT"
echo "Key: $SSH_KEY"
echo ""

# Function to run diagnostic tests
run_diagnostics() {
    test_num=1
    
    # Test 1: Check if SSH key files exist locally
    print_status "Test $((test_num++)): Checking local SSH key files"
    if [ -f "$SSH_KEY" ]; then
        print_success "Private key exists: $SSH_KEY"
    else
        print_error "Private key missing: $SSH_KEY"
        return 1
    fi
    
    if [ -f "$SSH_KEY.pub" ]; then
        print_success "Public key exists: $SSH_KEY.pub"
    else
        print_error "Public key missing: $SSH_KEY.pub"
        return 1
    fi
    
    # Test 2: Check local key permissions
    print_status "Test $((test_num++)): Checking local key permissions"
    private_perms=$(stat -f "%OLp" "$SSH_KEY" 2>/dev/null || stat -c "%a" "$SSH_KEY" 2>/dev/null)
    if [ "$private_perms" = "600" ]; then
        print_success "Private key permissions correct: $private_perms"
    else
        print_warning "Private key permissions: $private_perms (should be 600)"
        echo "Fixing permissions..."
        chmod 600 "$SSH_KEY"
        print_success "Fixed private key permissions"
    fi
    
    # Test 3: Test basic connectivity
    print_status "Test $((test_num++)): Testing basic connectivity"
    if ping -c 1 -W 3 "$SSH_HOST" >/dev/null 2>&1; then
        print_success "Host is reachable"
    else
        print_error "Host is not reachable"
        echo "Check network connectivity and host IP address"
        return 1
    fi
    
    # Test 4: Test SSH service
    print_status "Test $((test_num++)): Testing SSH service"
    if nc -z -w 3 "$SSH_HOST" "$SSH_PORT" 2>/dev/null; then
        print_success "SSH service is running on port $SSH_PORT"
    else
        print_error "SSH service is not accessible on port $SSH_PORT"
        echo "Check if SSH service is running and firewall allows connections"
        return 1
    fi
    
    # Test 5: Check remote .ssh directory and authorized_keys
    print_status "Test $((test_num++)): Checking remote SSH configuration"
    echo "This may require password authentication..."
    
    if ssh -o PasswordAuthentication=yes -o PubkeyAuthentication=no -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'test -d ~/.ssh' 2>/dev/null; then
        print_success "Remote .ssh directory exists"
        
        # Check .ssh directory permissions
        ssh_dir_perms=$(ssh -o PasswordAuthentication=yes -o PubkeyAuthentication=no -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'stat -f "%OLp" ~/.ssh 2>/dev/null || stat -c "%a" ~/.ssh 2>/dev/null')
        if [ "$ssh_dir_perms" = "700" ]; then
            print_success "Remote .ssh directory permissions correct: $ssh_dir_perms"
        else
            print_warning "Remote .ssh directory permissions: $ssh_dir_perms (should be 700)"
            ssh -o PasswordAuthentication=yes -o PubkeyAuthentication=no -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'chmod 700 ~/.ssh'
            print_success "Fixed remote .ssh directory permissions"
        fi
        
        # Check authorized_keys file
        if ssh -o PasswordAuthentication=yes -o PubkeyAuthentication=no -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'test -f ~/.ssh/authorized_keys' 2>/dev/null; then
            print_success "Remote authorized_keys file exists"
            
            # Check authorized_keys permissions
            auth_keys_perms=$(ssh -o PasswordAuthentication=yes -o PubkeyAuthentication=no -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'stat -f "%OLp" ~/.ssh/authorized_keys 2>/dev/null || stat -c "%a" ~/.ssh/authorized_keys 2>/dev/null')
            if [ "$auth_keys_perms" = "600" ]; then
                print_success "Remote authorized_keys permissions correct: $auth_keys_perms"
            else
                print_warning "Remote authorized_keys permissions: $auth_keys_perms (should be 600)"
                ssh -o PasswordAuthentication=yes -o PubkeyAuthentication=no -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'chmod 600 ~/.ssh/authorized_keys'
                print_success "Fixed remote authorized_keys permissions"
            fi
            
            # Check if our key is in authorized_keys
            our_key=$(cat "$SSH_KEY.pub")
            if ssh -o PasswordAuthentication=yes -o PubkeyAuthentication=no -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" "grep -q \"$(echo "$our_key" | cut -d' ' -f2)\" ~/.ssh/authorized_keys" 2>/dev/null; then
                print_success "Our public key is present in authorized_keys"
            else
                print_error "Our public key is NOT in authorized_keys"
                echo "Adding our key to authorized_keys..."
                echo "$our_key" | ssh -o PasswordAuthentication=yes -o PubkeyAuthentication=no -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'cat >> ~/.ssh/authorized_keys'
                print_success "Added our key to authorized_keys"
            fi
        else
            print_error "Remote authorized_keys file does not exist"
            echo "Creating authorized_keys file..."
            cat "$SSH_KEY.pub" | ssh -o PasswordAuthentication=yes -o PubkeyAuthentication=no -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'cat > ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys'
            print_success "Created authorized_keys file with our key"
        fi
    else
        print_error "Cannot access remote server or .ssh directory does not exist"
        echo "Creating .ssh directory and authorized_keys..."
        ssh -o PasswordAuthentication=yes -o PubkeyAuthentication=no -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'mkdir -p ~/.ssh && chmod 700 ~/.ssh'
        cat "$SSH_KEY.pub" | ssh -o PasswordAuthentication=yes -o PubkeyAuthentication=no -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'cat > ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys'
        print_success "Created .ssh directory and authorized_keys file"
    fi
    
    # Test 6: Test key authentication with verbose output
    print_status "Test $((test_num++)): Testing key authentication (verbose)"
    echo "Running: ssh -v -i $SSH_KEY -o PasswordAuthentication=no -p $SSH_PORT $SSH_USER@$SSH_HOST 'echo success'"
    echo ""
    
    if ssh -v -i "$SSH_KEY" -o PasswordAuthentication=no -o BatchMode=yes -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'echo "Key authentication successful"' 2>&1; then
        print_success "Key authentication is working!"
        return 0
    else
        print_error "Key authentication still failing"
        echo ""
        echo "Let's check SSH server configuration..."
    fi
    
    # Test 7: Check SSH server configuration
    print_status "Test $((test_num++)): Checking SSH server configuration"
    echo "Checking SSH server settings that might prevent key authentication..."
    
    sshd_config=$(ssh -o PasswordAuthentication=yes -o PubkeyAuthentication=no -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'sudo cat /etc/ssh/sshd_config 2>/dev/null || echo "Cannot access sshd_config"')
    
    if echo "$sshd_config" | grep -q "PubkeyAuthentication.*no"; then
        print_error "SSH server has PubkeyAuthentication disabled"
        echo "Enable with: sudo sed -i 's/PubkeyAuthentication no/PubkeyAuthentication yes/' /etc/ssh/sshd_config"
    elif echo "$sshd_config" | grep -q "PubkeyAuthentication.*yes"; then
        print_success "SSH server has PubkeyAuthentication enabled"
    else
        print_warning "PubkeyAuthentication setting not found (probably using default: yes)"
    fi
    
    if echo "$sshd_config" | grep -q "AuthorizedKeysFile.*none\|AuthorizedKeysFile.*#"; then
        print_error "SSH server has AuthorizedKeysFile disabled or commented"
    else
        print_success "SSH server AuthorizedKeysFile setting appears correct"
    fi
    
    # Test 8: Try alternative authentication methods
    print_status "Test $((test_num++)): Trying alternative approaches"
    
    echo "Trying different SSH options..."
    
    # Try without strict host key checking
    if ssh -i "$SSH_KEY" -o PasswordAuthentication=no -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'echo "Alternative method 1 works"' >/dev/null 2>&1; then
        print_success "Works with relaxed host key checking"
    else
        print_warning "Still fails with relaxed host key checking"
    fi
    
    # Try with different key format (remove and re-add to ssh-agent)
    if command -v ssh-add >/dev/null 2>&1; then
        print_status "Testing with ssh-agent"
        ssh-add -d "$SSH_KEY" 2>/dev/null || true
        ssh-add "$SSH_KEY" 2>/dev/null
        
        if ssh -o PasswordAuthentication=no -p "$SSH_PORT" "$SSH_USER@$SSH_HOST" 'echo "ssh-agent method works"' >/dev/null 2>&1; then
            print_success "Works with ssh-agent"
        else
            print_warning "Still fails with ssh-agent"
        fi
    fi
}

# Function to provide recommendations
provide_recommendations() {
    echo ""
    print_status "🔧 Troubleshooting Recommendations:"
    echo ""
    
    echo "1. Manual key verification:"
    echo "   ssh -o PasswordAuthentication=yes -p $SSH_PORT $SSH_USER@$SSH_HOST 'cat ~/.ssh/authorized_keys'"
    echo ""
    
    echo "2. Check SSH server logs on remote server:"
    echo "   ssh -o PasswordAuthentication=yes -p $SSH_PORT $SSH_USER@$SSH_HOST 'sudo tail -20 /var/log/auth.log'"
    echo ""
    
    echo "3. Restart SSH service on remote server:"
    echo "   ssh -o PasswordAuthentication=yes -p $SSH_PORT $SSH_USER@$SSH_HOST 'sudo systemctl restart ssh'"
    echo ""
    
    echo "4. Test with verbose local output:"
    echo "   ssh -vvv -i $SSH_KEY -p $SSH_PORT $SSH_USER@$SSH_HOST 'echo test'"
    echo ""
    
    echo "5. Generate new key if all else fails:"
    echo "   ssh-keygen -t rsa -b 4096 -f ~/.ssh/azurephotoflow-k8s-new"
    echo "   ssh-copy-id -i ~/.ssh/azurephotoflow-k8s-new.pub -p $SSH_PORT $SSH_USER@$SSH_HOST"
}

# Main execution
echo "Starting SSH diagnostics..."
echo ""

if run_diagnostics; then
    echo ""
    print_success "SSH authentication is working correctly!"
    echo ""
    echo "You can now run the remote deployment scripts:"
    echo "  ./scripts/prepare-microk8s-remote.sh"
    echo "  ./scripts/deploy-k8s-remote.sh production latest"
else
    echo ""
    print_error "SSH authentication issues detected"
    provide_recommendations
fi