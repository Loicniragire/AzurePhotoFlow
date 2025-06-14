#!/bin/bash

# Remote MicroK8s cluster preparation script for AzurePhotoFlow
# This script connects to a remote MicroK8s cluster via SSH and prepares it for deployment

set -e

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default SSH settings
SSH_USER=${SSH_USER:-""}
SSH_HOST=${SSH_HOST:-""}
SSH_PORT=${SSH_PORT:-22}
SSH_KEY=${SSH_KEY:-""}
SSH_OPTIONS="-o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR -o ControlMaster=auto -o ControlPath=~/.ssh/control-%r@%h:%p -o ControlPersist=10m"

# Function to print colored output
print_status() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

print_header() {
    echo ""
    print_status $BLUE "=== $1 ==="
}

print_success() {
    print_status $GREEN "✅ $1"
}

print_warning() {
    print_status $YELLOW "⚠️  $1"
}

print_error() {
    print_status $RED "❌ $1"
}

# Function to show usage
show_usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  -h, --host HOST       Remote server hostname/IP (required)"
    echo "  -u, --user USER       SSH username (required)"
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
    echo "  $0 -h k8s-server.local -u admin -k ~/.ssh/k8s_key"
    echo "  SSH_HOST=192.168.1.100 SSH_USER=ubuntu $0"
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

# Validate required parameters
if [ -z "$SSH_HOST" ] || [ -z "$SSH_USER" ]; then
    print_error "SSH host and user are required"
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
        print_status $BLUE "$description"
    fi
    
    $SSH_CMD "$command"
}

# Function to check SSH connectivity
check_ssh_connection() {
    print_header "Checking SSH connection"
    
    if remote_exec "echo 'SSH connection successful'" "Testing SSH connection..."; then
        print_success "SSH connection established"
    else
        print_error "Cannot establish SSH connection"
        echo "Please check:"
        echo "- Host: $SSH_HOST"
        echo "- User: $SSH_USER" 
        echo "- Port: $SSH_PORT"
        if [ -n "$SSH_KEY" ]; then
            echo "- Key: $SSH_KEY"
        fi
        exit 1
    fi
    
    # Get basic system info
    echo ""
    echo "Remote system information:"
    remote_exec "uname -a" ""
    remote_exec "whoami" "Current user: "
}

# Function to check MicroK8s on remote system
check_remote_microk8s() {
    print_header "Checking remote MicroK8s installation"
    
    # Check if MicroK8s is installed
    if remote_exec "command -v microk8s >/dev/null 2>&1 && echo 'installed' || echo 'not installed'" "" | grep -q "installed"; then
        print_success "MicroK8s is installed on remote system"
    else
        print_error "MicroK8s is not installed on remote system"
        echo ""
        echo "To install MicroK8s on the remote server:"
        echo "  ssh $SSH_USER@$SSH_HOST 'sudo snap install microk8s --classic'"
        echo "  ssh $SSH_USER@$SSH_HOST 'sudo usermod -a -G microk8s \$USER'"
        echo "  # Then reconnect SSH session"
        return 1
    fi
    
    # Check MicroK8s status
    if remote_exec "microk8s status --wait-ready --timeout=30 >/dev/null 2>&1 && echo 'ready' || echo 'not ready'" "" | grep -q "ready"; then
        print_success "MicroK8s is running and ready"
    else
        print_error "MicroK8s is not ready"
        echo "Start MicroK8s on remote server:"
        echo "  ssh $SSH_USER@$SSH_HOST 'microk8s start'"
        return 1
    fi
    
    # Show cluster info
    echo ""
    echo "Remote MicroK8s cluster info:"
    remote_exec "microk8s kubectl cluster-info" ""
}

# Function to check if addon is enabled
check_addon_enabled() {
    local addon_name="$1"
    local status_output=$(remote_exec "microk8s status 2>/dev/null" "")
    
    # Check if the addon appears as "enabled" in the status output
    echo "$status_output" | grep -q "^${addon_name}: enabled" && return 0
    
    # Handle MicroK8s addon name variations
    case "$addon_name" in
        "storage")
            # "storage" is actually "hostpath-storage" in MicroK8s
            echo "$status_output" | grep -q "^hostpath-storage: enabled" && return 0
            ;;
        "dns")
            # Check both "dns" and potential variations
            echo "$status_output" | grep -q "^dns: enabled" && return 0
            echo "$status_output" | grep -q "^coredns: enabled" && return 0
            ;;
        "cert-manager")
            # Check for cert-manager variations
            echo "$status_output" | grep -q "^cert-manager: enabled" && return 0
            ;;
        "metrics-server")
            # Check for metrics-server variations
            echo "$status_output" | grep -q "^metrics-server: enabled" && return 0
            ;;
        "ingress")
            # Check for ingress variations
            echo "$status_output" | grep -q "^ingress: enabled" && return 0
            echo "$status_output" | grep -q "^nginx-ingress: enabled" && return 0
            ;;
    esac
    
    return 1
}

# Function to check MicroK8s addons on remote system
check_remote_addons() {
    print_header "Checking remote MicroK8s addons"
    
    echo "Current addon status on remote server:"
    remote_exec "microk8s status" ""
    echo ""
    
    # Check required addons
    local required_addons=("dns" "storage")
    local missing_required=()
    
    print_status $BLUE "Required addons:"
    for addon in "${required_addons[@]}"; do
        if check_addon_enabled "$addon"; then
            print_success "$addon is enabled"
        else
            print_error "$addon is not enabled"
            echo "Enable with: ssh $SSH_USER@$SSH_HOST 'microk8s enable $addon'"
            missing_required+=("$addon")
        fi
    done
    
    # Check optional addons
    local optional_addons=("ingress" "cert-manager" "metrics-server")
    local missing_optional=()
    
    echo ""
    print_status $BLUE "Optional but recommended addons:"
    for addon in "${optional_addons[@]}"; do
        if check_addon_enabled "$addon"; then
            print_success "$addon is enabled"
        else
            print_warning "$addon is not enabled (optional)"
            echo "Enable with: ssh $SSH_USER@$SSH_HOST 'microk8s enable $addon'"
            missing_optional+=("$addon")
        fi
    done
    
    # Show commands to enable missing addons
    if [ ${#missing_required[@]} -gt 0 ] || [ ${#missing_optional[@]} -gt 0 ]; then
        echo ""
        print_status $YELLOW "To enable missing addons on remote server:"
        
        if [ ${#missing_required[@]} -gt 0 ]; then
            echo "Required addons:"
            for addon in "${missing_required[@]}"; do
                echo "  ssh $SSH_USER@$SSH_HOST 'microk8s enable $addon'"
            done
        fi
        
        if [ ${#missing_optional[@]} -gt 0 ]; then
            echo "Optional addons:"
            for addon in "${missing_optional[@]}"; do
                echo "  ssh $SSH_USER@$SSH_HOST 'microk8s enable $addon'"
            done
        fi
        
        echo ""
        echo "Or enable all at once:"
        local all_missing=(${missing_required[@]} ${missing_optional[@]})
        echo "  ssh $SSH_USER@$SSH_HOST 'microk8s enable ${all_missing[*]}'"
    fi
}

# Function to check storage on remote system
check_remote_storage() {
    print_header "Checking remote storage configuration"
    
    if check_addon_enabled "storage"; then
        print_success "MicroK8s storage addon is enabled (hostpath-storage)"
        
        echo ""
        echo "Available storage classes:"
        remote_exec "microk8s kubectl get storageclass" ""
        
        # Check for default storage class
        local default_sc=$(remote_exec "microk8s kubectl get storageclass -o jsonpath='{.items[?(@.metadata.annotations.storageclass\\.kubernetes\\.io/is-default-class==\"true\")].metadata.name}' 2>/dev/null" "")
        if [ -n "$default_sc" ]; then
            print_success "Default storage class: $default_sc"
        else
            print_warning "No default storage class found"
        fi
    else
        print_error "MicroK8s storage addon not enabled"
        echo "Enable with: ssh $SSH_USER@$SSH_HOST 'microk8s enable storage'"
        return 1
    fi
}

# Function to setup local kubectl for remote cluster
setup_local_kubectl() {
    print_header "Setting up local kubectl for remote cluster"
    
    # Create backup of existing config
    if [ -f "$HOME/.kube/config" ]; then
        print_warning "Backing up existing kubectl config"
        cp "$HOME/.kube/config" "$HOME/.kube/config.backup.$(date +%s)"
    fi
    
    # Create .kube directory if it doesn't exist
    mkdir -p "$HOME/.kube"
    
    # Get remote cluster config
    print_status $BLUE "Downloading cluster config from remote server..."
    if remote_exec "microk8s config" "" > "$HOME/.kube/config.tmp"; then
        
        # Replace localhost/127.0.0.1 with actual server IP
        sed "s/127.0.0.1/$SSH_HOST/g; s/localhost/$SSH_HOST/g" "$HOME/.kube/config.tmp" > "$HOME/.kube/config"
        rm "$HOME/.kube/config.tmp"
        
        print_success "kubectl config downloaded and updated"
        
        # Test kubectl connectivity
        if kubectl get nodes >/dev/null 2>&1; then
            print_success "Local kubectl can connect to remote cluster"
            echo ""
            echo "Cluster nodes:"
            kubectl get nodes -o wide
        else
            print_error "Local kubectl cannot connect to remote cluster"
            echo "You may need to use SSH tunneling or check firewall settings"
            return 1
        fi
    else
        print_error "Failed to download cluster config"
        return 1
    fi
}

# Function to test PVC creation on remote cluster
test_remote_pvc_creation() {
    print_header "Testing PVC creation on remote cluster"
    
    local test_pvc_name="test-pvc-remote-$(date +%s)"
    
    # Create test PVC on remote cluster
    cat <<EOF | remote_exec "microk8s kubectl apply -f -" "Creating test PVC..."
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: $test_pvc_name
  namespace: default
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 1Gi
EOF

    # Wait for PVC to be bound
    echo "Waiting for PVC to be bound..."
    if remote_exec "microk8s kubectl wait --for=condition=bound pvc/$test_pvc_name --timeout=60s >/dev/null 2>&1" ""; then
        print_success "PVC creation and binding successful"
        
        # Show PVC details
        remote_exec "microk8s kubectl get pvc $test_pvc_name" "PVC details:"
    else
        print_error "PVC failed to bind within 60 seconds"
        remote_exec "microk8s kubectl describe pvc $test_pvc_name" "PVC details:"
    fi
    
    # Clean up
    remote_exec "microk8s kubectl delete pvc $test_pvc_name >/dev/null 2>&1 || true" "Cleaning up test PVC..."
}

# Function to show remote setup commands
show_remote_setup_commands() {
    print_header "Remote MicroK8s setup commands"
    
    echo "SSH into remote server:"
    echo "  ssh $SSH_USER@$SSH_HOST"
    echo ""
    
    echo "Enable required addons:"
    echo "  microk8s enable dns storage"
    echo ""
    
    echo "Enable recommended addons:"
    echo "  microk8s enable ingress cert-manager metrics-server"
    echo ""
    
    echo "Optional addons for development:"
    echo "  microk8s enable registry dashboard"
    echo ""
    
    echo "Check addon status:"
    echo "  microk8s status"
    echo ""
    
    echo "Setup MetalLB (for LoadBalancer services):"
    echo "  microk8s enable metallb:START_IP-END_IP"
    echo "  # Example: microk8s enable metallb:192.168.1.240-192.168.1.250"
}

# Main execution
main() {
    print_status $BLUE "🚀 Remote MicroK8s Cluster Preparation for AzurePhotoFlow"
    print_status $BLUE "========================================================="
    echo ""
    echo "Connecting to: $SSH_USER@$SSH_HOST:$SSH_PORT"
    if [ -n "$SSH_KEY" ]; then
        echo "Using SSH key: $SSH_KEY"
    fi
    echo ""
    
    local exit_code=0
    
    # Run all checks
    check_ssh_connection || exit_code=1
    
    if [ $exit_code -eq 0 ]; then
        check_remote_microk8s || exit_code=1
        check_remote_addons
        check_remote_storage || exit_code=1
        
        # Only setup local kubectl if remote checks pass
        if [ $exit_code -eq 0 ]; then
            setup_local_kubectl
            test_remote_pvc_creation
        fi
    fi
    
    # Summary
    print_header "Summary"
    
    if [ $exit_code -eq 0 ]; then
        print_success "Remote MicroK8s cluster is ready for AzurePhotoFlow deployment!"
        echo ""
        print_status $GREEN "✅ Next steps:"
        echo "1. Configure secrets: ./scripts/setup-secrets.sh"
        echo "2. Update domain configuration in k8s/configmap.yaml and k8s/ingress-microk8s.yaml"
        echo "3. Deploy application: ./scripts/deploy-k8s-remote.sh production latest"
    else
        print_error "Remote MicroK8s cluster needs additional configuration"
        echo ""
        show_remote_setup_commands
    fi
    
    return $exit_code
}

# Run main function with all arguments
main "$@"