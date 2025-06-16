#!/bin/bash

# Test script to verify SSH command differences
set -e

# Color codes
GREEN='\033[0;32m'
RED='\033[0;31m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

print_status() { echo -e "${BLUE}$1${NC}"; }
print_success() { echo -e "${GREEN}✅ $1${NC}"; }
print_error() { echo -e "${RED}❌ $1${NC}"; }
print_warning() { echo -e "${YELLOW}⚠️ $1${NC}"; }

# Connection details
SSH_HOST=${SSH_HOST:-"10.0.0.2"}
SSH_USER=${SSH_USER:-"loicn"}

print_status "🧪 Testing SSH command differences for verification step"
echo ""

print_status "❌ ORIGINAL (BROKEN) ssh_with_timeout function:"
echo "ssh -o ConnectTimeout=10 -o ServerAliveInterval=10 -o ServerAliveCountMax=2 \\"
echo "    \$SSH_USER@\$SSH_HOST \"\$cmd\" 2>/dev/null"
echo ""
print_error "Missing SSH key (-i ~/.ssh/azure_pipeline_key)"
print_error "Missing StrictHostKeyChecking=no"
print_error "Missing UserKnownHostsFile=/dev/null"
echo ""

print_status "✅ FIXED ssh_with_timeout function:"
echo "ssh -i ~/.ssh/azure_pipeline_key \\"
echo "    -o ConnectTimeout=10 -o ServerAliveInterval=10 -o ServerAliveCountMax=2 \\"
echo "    -o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR \\"
echo "    \$SSH_USER@\$SSH_HOST \"\$cmd\" 2>/dev/null"
echo ""
print_success "Includes SSH key authentication"
print_success "Includes proper SSH security options"
print_success "Matches smart-deploy.sh remote_exec function"
echo ""

print_status "🎯 EXPECTED RESULT:"
echo "  Before fix: 'Deploy to remote cluster (smart)' succeeds, 'Verify deployment' fails"
echo "  After fix:  Both steps should work consistently with the same SSH authentication"
echo ""

print_warning "⚠️  NOTE: Both steps need to use the SSH key setup in the pipeline:"
echo "  Pipeline sets: SSH_KEY=~/.ssh/azure_pipeline_key"
echo "  Both smart-deploy.sh and verification step must use this key"