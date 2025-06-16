#!/bin/bash

# Quick MicroK8s Cluster Restart Script
# Usage: ./scripts/restart-cluster.sh [mode]
# Modes: basic, hard, reset

set -e

# Color codes
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m'

print_status() { echo -e "${BLUE}$1${NC}"; }
print_success() { echo -e "${GREEN}✅ $1${NC}"; }
print_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }
print_error() { echo -e "${RED}❌ $1${NC}"; }

# Load SSH environment
SSH_ENV_FILE="$HOME/.ssh_env"
if [ -f "$SSH_ENV_FILE" ]; then
    source "$SSH_ENV_FILE"
fi

# Default values if not set
SSH_HOST=${SSH_HOST:-"10.0.0.2"}
SSH_USER=${SSH_USER:-"loicn"}

MODE=${1:-"basic"}

print_status "🔄 Restarting MicroK8s cluster on $SSH_USER@$SSH_HOST"
print_status "Mode: $MODE"

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

case "$MODE" in
    "basic")
        print_status "Performing basic MicroK8s restart..."
        remote_exec "microk8s stop" "Stopping MicroK8s..."
        print_status "Waiting 10 seconds..."
        sleep 10
        remote_exec "microk8s start" "Starting MicroK8s..."
        print_status "Waiting for startup..."
        sleep 15
        ;;
        
    "hard")
        print_status "Performing hard restart with service reset..."
        remote_exec "microk8s stop" "Stopping MicroK8s..."
        remote_exec "sudo systemctl restart snap.microk8s.daemon-apiserver" "Restarting API server..."
        remote_exec "sudo systemctl restart snap.microk8s.daemon-controller-manager" "Restarting controller manager..."
        remote_exec "sudo systemctl restart snap.microk8s.daemon-scheduler" "Restarting scheduler..."
        remote_exec "sudo systemctl restart snap.microk8s.daemon-kubelet" "Restarting kubelet..."
        sleep 10
        remote_exec "microk8s start" "Starting MicroK8s..."
        sleep 20
        ;;
        
    "reset")
        print_warning "WARNING: This will destroy all deployments and data!"
        read -p "Are you sure? (yes/no): " confirm
        if [ "$confirm" = "yes" ]; then
            print_status "Performing destructive reset..."
            remote_exec "microk8s reset --destructive-addons" "Resetting cluster..."
            sleep 10
            remote_exec "microk8s enable dns storage ingress" "Re-enabling basic addons..."
            sleep 15
        else
            print_status "Reset cancelled"
            exit 0
        fi
        ;;
        
    *)
        print_error "Unknown mode: $MODE"
        print_status "Available modes: basic, hard, reset"
        exit 1
        ;;
esac

# Test cluster responsiveness
print_status "Testing cluster responsiveness..."
if remote_exec "microk8s kubectl get nodes" "Getting cluster nodes..." 2>/dev/null; then
    print_success "Cluster restart successful!"
    remote_exec "microk8s status" "Cluster status:"
else
    print_warning "Cluster may need more time to start up"
    print_status "You can check status with: ssh $SSH_USER@$SSH_HOST 'microk8s status'"
fi

print_success "Restart operation completed"