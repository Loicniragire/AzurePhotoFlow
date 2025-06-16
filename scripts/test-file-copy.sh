#!/bin/bash

# Test script to verify file copying works with SSH key
set -e

# Color codes
GREEN='\033[0;32m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m'

print_status() { echo -e "${BLUE}$1${NC}"; }
print_success() { echo -e "${GREEN}✅ $1${NC}"; }
print_error() { echo -e "${RED}❌ $1${NC}"; }

# Connection details
SSH_HOST=${SSH_HOST:-"10.0.0.2"}
SSH_USER=${SSH_USER:-"loicn"}

print_status "🧪 Testing file copy functionality to $SSH_USER@$SSH_HOST"

# Test 1: Basic SSH connectivity
print_status "Test 1: Basic SSH connectivity"
if ssh -o ConnectTimeout=10 -o BatchMode=yes "$SSH_USER@$SSH_HOST" 'echo "SSH OK"' 2>/dev/null; then
    print_success "SSH connection working"
else
    print_error "SSH connection failed"
    exit 1
fi

# Test 2: Create test directory
test_dir="/tmp/test-k8s-copy-$(date +%s)"
print_status "Test 2: Creating test directory $test_dir"
if ssh -o ConnectTimeout=10 "$SSH_USER@$SSH_HOST" "mkdir -p $test_dir" 2>/dev/null; then
    print_success "Test directory created"
else
    print_error "Failed to create test directory"
    exit 1
fi

# Test 3: Copy k8s directory
print_status "Test 3: Copying k8s directory"
if scp -o ConnectTimeout=10 -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -r k8s/ "$SSH_USER@$SSH_HOST:$test_dir/"; then
    print_success "Files copied successfully"
else
    print_error "File copy failed"
    ssh -o ConnectTimeout=10 "$SSH_USER@$SSH_HOST" "rm -rf $test_dir" 2>/dev/null || true
    exit 1
fi

# Test 4: Verify files exist
print_status "Test 4: Verifying copied files"
echo "Files in remote directory:"
ssh -o ConnectTimeout=10 "$SSH_USER@$SSH_HOST" "ls -la $test_dir/" 2>/dev/null || {
    print_error "Failed to list remote directory"
    exit 1
}

# Test 5: Check specific files
print_status "Test 5: Checking for required manifest files"
required_files=("namespace.yaml" "configmap.yaml" "app/" "storage/")
for file in "${required_files[@]}"; do
    if ssh -o ConnectTimeout=10 "$SSH_USER@$SSH_HOST" "test -e $test_dir/$file" 2>/dev/null; then
        print_success "$file exists"
    else
        print_error "$file missing"
    fi
done

# Cleanup
print_status "Cleaning up test directory..."
ssh -o ConnectTimeout=10 "$SSH_USER@$SSH_HOST" "rm -rf $test_dir" 2>/dev/null || true

print_success "🎉 File copy test completed successfully!"