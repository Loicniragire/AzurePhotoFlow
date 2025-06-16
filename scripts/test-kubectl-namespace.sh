#!/bin/bash

# Quick test script to debug kubectl namespace creation issues

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

# Connection details
SSH_HOST=${SSH_HOST:-"10.0.0.2"}
SSH_USER=${SSH_USER:-"loicn"}
NAMESPACE="azurephotoflow"

print_status "🔍 Testing kubectl namespace operations on $SSH_USER@$SSH_HOST"
echo ""

# Test 1: Basic connectivity
print_status "Test 1: Basic SSH connectivity"
if ssh -o ConnectTimeout=10 -o BatchMode=yes "$SSH_USER@$SSH_HOST" 'echo "SSH OK"' 2>/dev/null; then
    print_success "SSH connection working"
else
    print_error "SSH connection failed"
    exit 1
fi

# Test 2: MicroK8s availability
print_status "Test 2: MicroK8s command availability"
MICROK8S_CMD=""
for cmd in "microk8s" "/snap/bin/microk8s" "sudo microk8s"; do
    print_status "  Trying: $cmd"
    if ssh -o ConnectTimeout=10 "$SSH_USER@$SSH_HOST" "$cmd version --short" 2>/dev/null; then
        MICROK8S_CMD="$cmd"
        print_success "  Working: $cmd"
        break
    else
        print_warning "  Failed: $cmd"
    fi
done

if [ -z "$MICROK8S_CMD" ]; then
    print_error "No working MicroK8s command found"
    exit 1
fi

# Test 3: kubectl basic functionality
print_status "Test 3: kubectl basic functionality"
if ssh -o ConnectTimeout=10 "$SSH_USER@$SSH_HOST" "$MICROK8S_CMD kubectl get nodes" 2>/dev/null; then
    print_success "kubectl get nodes working"
else
    print_error "kubectl get nodes failed"
    exit 1
fi

# Test 4: Check if namespace already exists
print_status "Test 4: Check if namespace already exists"
if ssh -o ConnectTimeout=10 "$SSH_USER@$SSH_HOST" "$MICROK8S_CMD kubectl get namespace $NAMESPACE" 2>/dev/null; then
    print_warning "Namespace $NAMESPACE already exists"
    
    read -p "Delete existing namespace for testing? (y/N): " delete_ns
    if [[ "$delete_ns" =~ ^[Yy]$ ]]; then
        print_status "Deleting existing namespace..."
        ssh -o ConnectTimeout=10 "$SSH_USER@$SSH_HOST" "$MICROK8S_CMD kubectl delete namespace $NAMESPACE" || print_warning "Delete failed"
        sleep 5
    fi
else
    print_success "Namespace $NAMESPACE does not exist (ready for creation)"
fi

# Test 5: Create namespace with timeout
print_status "Test 5: Creating namespace with manual timeout"
echo "Running command: $MICROK8S_CMD kubectl create namespace $NAMESPACE"

# Manual timeout implementation
{
    ssh -o ConnectTimeout=10 -o ServerAliveInterval=10 -o ServerAliveCountMax=3 \
        "$SSH_USER@$SSH_HOST" "$MICROK8S_CMD kubectl create namespace $NAMESPACE" &
    SSH_PID=$!
    
    # Wait with timeout
    for i in {1..30}; do
        if ! kill -0 $SSH_PID 2>/dev/null; then
            wait $SSH_PID
            exit_code=$?
            if [ $exit_code -eq 0 ]; then
                print_success "Namespace created successfully in ${i} seconds"
            else
                print_error "Namespace creation failed with exit code $exit_code"
            fi
            break
        fi
        echo -n "."
        sleep 1
        if [ $i -eq 30 ]; then
            print_error "Command timed out after 30 seconds"
            kill $SSH_PID 2>/dev/null || true
            exit 1
        fi
    done
}

# Test 6: Verify namespace was created
print_status "Test 6: Verify namespace creation"
if ssh -o ConnectTimeout=10 "$SSH_USER@$SSH_HOST" "$MICROK8S_CMD kubectl get namespace $NAMESPACE" 2>/dev/null; then
    print_success "Namespace $NAMESPACE verified successfully"
else
    print_error "Namespace $NAMESPACE not found after creation"
    exit 1
fi

print_success "🎉 All tests passed! Namespace operations are working correctly."
echo ""
print_status "💡 Recommended command for your environment:"
echo "  $MICROK8S_CMD kubectl create namespace $NAMESPACE"