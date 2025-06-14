#!/bin/bash

# SSH Helper Script for AzurePhotoFlow Remote Deployment
# This script provides utilities for managing SSH connections to remote servers

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

# Default SSH settings
SSH_USER=${SSH_USER:-""}
SSH_HOST=${SSH_HOST:-""}
SSH_PORT=${SSH_PORT:-22}
SSH_KEY=${SSH_KEY:-""}
SSH_OPTIONS="-o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR"

# Function to show usage
show_usage() {
    echo "SSH Helper for AzurePhotoFlow Remote Deployment"
    echo ""
    echo "Usage: $0 [COMMAND] [OPTIONS]"
    echo ""
    echo "Commands:"
    echo "  connect                Connect to remote server interactively"
    echo "  test                   Test SSH connection"
    echo "  exec \"command\"         Execute command on remote server"
    echo "  copy <local> <remote>  Copy file/directory to remote server"
    echo "  tunnel <port>          Create SSH tunnel for port forwarding"
    echo "  env                    Show current SSH environment variables"
    echo "  setup                  Setup SSH connection (runs setup-ssh-keys.sh)"
    echo ""
    echo "Options:"
    echo "  -h, --host HOST        Remote server hostname/IP"
    echo "  -u, --user USER        SSH username"
    echo "  -p, --port PORT        SSH port (default: 22)"
    echo "  -k, --key KEY_PATH     SSH private key path"
    echo "  --help                 Show this help message"
    echo ""
    echo "Environment variables:"
    echo "  SSH_HOST               Remote server hostname/IP"
    echo "  SSH_USER               SSH username"
    echo "  SSH_PORT               SSH port (default: 22)"
    echo "  SSH_KEY                SSH private key path"
    echo ""
    echo "Examples:"
    echo "  $0 test -h 192.168.1.100 -u ubuntu"
    echo "  $0 exec \"microk8s status\" -h 192.168.1.100 -u ubuntu"
    echo "  $0 copy k8s/ /tmp/deployment/"
    echo "  $0 tunnel 8080  # Forward local 8080 to remote service"
    echo ""
    echo "Quick setup:"
    echo "  $0 setup -h 192.168.1.100 -u ubuntu"
    echo "  source ~/.azurephotoflow-ssh.env"
    echo "  $0 test"
}

# Parse command line arguments
COMMAND=""
EXEC_COMMAND=""
COPY_LOCAL=""
COPY_REMOTE=""
TUNNEL_PORT=""

if [[ $# -eq 0 ]]; then
    show_usage
    exit 0
fi

# Parse first argument as command
case "$1" in
    connect|test|exec|copy|tunnel|env|setup)
        COMMAND="$1"
        shift
        ;;
    --help|-help|help)
        show_usage
        exit 0
        ;;
    *)
        COMMAND="$1"
        shift
        ;;
esac

# Handle command-specific arguments
case "$COMMAND" in
    exec)
        if [[ $# -gt 0 && ! "$1" =~ ^- ]]; then
            EXEC_COMMAND="$1"
            shift
        fi
        ;;
    copy)
        if [[ $# -gt 1 ]]; then
            COPY_LOCAL="$1"
            COPY_REMOTE="$2"
            shift 2
        fi
        ;;
    tunnel)
        if [[ $# -gt 0 && ! "$1" =~ ^- ]]; then
            TUNNEL_PORT="$1"
            shift
        fi
        ;;
esac

# Parse remaining options
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

# Load environment file if it exists and no SSH_HOST is set
if [ -z "$SSH_HOST" ] && [ -f "$HOME/.azurephotoflow-ssh.env" ]; then
    print_status "Loading SSH configuration from ~/.azurephotoflow-ssh.env"
    source "$HOME/.azurephotoflow-ssh.env"
fi

# Build SSH command
build_ssh_command() {
    local ssh_cmd="ssh"
    
    # Add key if specified
    if [ -n "$SSH_KEY" ]; then
        ssh_cmd="$ssh_cmd -i $SSH_KEY"
    fi
    
    # Add standard options
    ssh_cmd="$ssh_cmd $SSH_OPTIONS"
    
    # Add port
    ssh_cmd="$ssh_cmd -p $SSH_PORT"
    
    # Add connection multiplexing for performance
    ssh_cmd="$ssh_cmd -o ControlMaster=auto -o ControlPath=~/.ssh/control-%r@%h:%p -o ControlPersist=10m"
    
    echo "$ssh_cmd $SSH_USER@$SSH_HOST"
}

# Function to test SSH connection
test_connection() {
    print_status "Testing SSH connection to $SSH_USER@$SSH_HOST:$SSH_PORT"
    
    if [ -z "$SSH_HOST" ] || [ -z "$SSH_USER" ]; then
        print_error "SSH_HOST and SSH_USER must be set"
        return 1
    fi
    
    local ssh_cmd=$(build_ssh_command)
    
    if $ssh_cmd 'echo "SSH connection successful"' >/dev/null 2>&1; then
        print_success "SSH connection is working"
        
        # Test MicroK8s access
        if $ssh_cmd 'command -v microk8s >/dev/null 2>&1' >/dev/null 2>&1; then
            print_success "MicroK8s is available on remote server"
            
            if $ssh_cmd 'microk8s status --wait-ready --timeout=10 >/dev/null 2>&1' >/dev/null 2>&1; then
                print_success "MicroK8s is ready"
            else
                print_warning "MicroK8s is installed but not ready"
            fi
        else
            print_warning "MicroK8s not found on remote server"
        fi
        
        return 0
    else
        print_error "SSH connection failed"
        echo ""
        echo "Troubleshooting:"
        echo "1. Check if the server is accessible: ping $SSH_HOST"
        echo "2. Verify SSH service is running on the server"
        echo "3. Check your credentials and key path"
        echo "4. Run setup: $0 setup -h $SSH_HOST -u $SSH_USER"
        return 1
    fi
}

# Function to connect interactively
connect_interactive() {
    if [ -z "$SSH_HOST" ] || [ -z "$SSH_USER" ]; then
        print_error "SSH_HOST and SSH_USER must be set"
        return 1
    fi
    
    print_status "Connecting to $SSH_USER@$SSH_HOST:$SSH_PORT"
    local ssh_cmd=$(build_ssh_command)
    $ssh_cmd
}

# Function to execute remote command
execute_remote() {
    if [ -z "$EXEC_COMMAND" ]; then
        print_error "No command specified"
        echo "Usage: $0 exec \"command\" [options]"
        return 1
    fi
    
    if [ -z "$SSH_HOST" ] || [ -z "$SSH_USER" ]; then
        print_error "SSH_HOST and SSH_USER must be set"
        return 1
    fi
    
    print_status "Executing: $EXEC_COMMAND"
    local ssh_cmd=$(build_ssh_command)
    $ssh_cmd "$EXEC_COMMAND"
}

# Function to copy files
copy_files() {
    if [ -z "$COPY_LOCAL" ] || [ -z "$COPY_REMOTE" ]; then
        print_error "Source and destination must be specified"
        echo "Usage: $0 copy <local-path> <remote-path> [options]"
        return 1
    fi
    
    if [ -z "$SSH_HOST" ] || [ -z "$SSH_USER" ]; then
        print_error "SSH_HOST and SSH_USER must be set"
        return 1
    fi
    
    print_status "Copying $COPY_LOCAL to $SSH_USER@$SSH_HOST:$COPY_REMOTE"
    
    local scp_cmd="scp"
    
    # Add key if specified
    if [ -n "$SSH_KEY" ]; then
        scp_cmd="$scp_cmd -i $SSH_KEY"
    fi
    
    # Add standard options (adapted for scp)
    scp_cmd="$scp_cmd -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR"
    
    # Add port
    scp_cmd="$scp_cmd -P $SSH_PORT"
    
    # Check if source is directory
    if [ -d "$COPY_LOCAL" ]; then
        scp_cmd="$scp_cmd -r"
    fi
    
    $scp_cmd "$COPY_LOCAL" "$SSH_USER@$SSH_HOST:$COPY_REMOTE"
}

# Function to create SSH tunnel
create_tunnel() {
    if [ -z "$TUNNEL_PORT" ]; then
        print_error "Port must be specified"
        echo "Usage: $0 tunnel <port> [options]"
        return 1
    fi
    
    if [ -z "$SSH_HOST" ] || [ -z "$SSH_USER" ]; then
        print_error "SSH_HOST and SSH_USER must be set"
        return 1
    fi
    
    print_status "Creating SSH tunnel: localhost:$TUNNEL_PORT -> $SSH_HOST:$TUNNEL_PORT"
    print_status "Access the service at: http://localhost:$TUNNEL_PORT"
    print_status "Press Ctrl+C to close the tunnel"
    
    local ssh_cmd=$(build_ssh_command)
    $ssh_cmd -L "$TUNNEL_PORT:localhost:$TUNNEL_PORT" -N
}

# Function to show environment
show_environment() {
    echo "Current SSH Environment:"
    echo "  SSH_HOST: ${SSH_HOST:-'(not set)'}"
    echo "  SSH_USER: ${SSH_USER:-'(not set)'}"
    echo "  SSH_PORT: ${SSH_PORT:-22}"
    echo "  SSH_KEY: ${SSH_KEY:-'(not set)'}"
    echo ""
    
    if [ -f "$HOME/.azurephotoflow-ssh.env" ]; then
        echo "Environment file exists: ~/.azurephotoflow-ssh.env"
        echo "To load: source ~/.azurephotoflow-ssh.env"
    else
        echo "Environment file not found: ~/.azurephotoflow-ssh.env"
        echo "Run: $0 setup -h HOST -u USER"
    fi
    
    echo ""
    if [ -n "$SSH_HOST" ] && [ -n "$SSH_USER" ]; then
        echo "SSH command that would be used:"
        echo "  $(build_ssh_command)"
    fi
}

# Function to run setup
run_setup() {
    local setup_script="$(dirname "$0")/setup-ssh-keys.sh"
    
    if [ ! -f "$setup_script" ]; then
        print_error "Setup script not found: $setup_script"
        return 1
    fi
    
    local setup_args=""
    
    if [ -n "$SSH_HOST" ]; then
        setup_args="$setup_args -h $SSH_HOST"
    fi
    
    if [ -n "$SSH_USER" ]; then
        setup_args="$setup_args -u $SSH_USER"
    fi
    
    if [ -n "$SSH_PORT" ] && [ "$SSH_PORT" != "22" ]; then
        setup_args="$setup_args -p $SSH_PORT"
    fi
    
    print_status "Running SSH setup..."
    "$setup_script" $setup_args
}

# Main execution
case "$COMMAND" in
    "test")
        test_connection
        ;;
    "connect")
        connect_interactive
        ;;
    "exec")
        execute_remote
        ;;
    "copy")
        copy_files
        ;;
    "tunnel")
        create_tunnel
        ;;
    "env")
        show_environment
        ;;
    "setup")
        run_setup
        ;;
    *)
        print_error "Unknown command: $COMMAND"
        echo ""
        show_usage
        exit 1
        ;;
esac