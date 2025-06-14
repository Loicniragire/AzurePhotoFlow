#!/bin/bash

# Pipeline Deployment Setup Script
# This script helps configure Azure DevOps pipeline variables for remote deployment

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

echo ""
print_header "🔧 Azure DevOps Pipeline Deployment Setup"
echo ""
print_info "This script will help you configure Azure DevOps pipeline variables for remote deployment."
echo ""

# Check if SSH key exists
SSH_KEY_PATH="$HOME/.ssh/azurephotoflow-k8s"
if [ ! -f "$SSH_KEY_PATH" ]; then
    print_error "SSH key not found at $SSH_KEY_PATH"
    echo ""
    echo "Please run the remote setup first:"
    echo "  ./deploy-remote.sh"
    echo "Or:"
    echo "  ./scripts/setup-ssh-keys.sh -h YOUR_SERVER_IP -u YOUR_USERNAME"
    exit 1
fi

# Load existing SSH environment if available
if [ -f "$HOME/.azurephotoflow-ssh.env" ]; then
    print_info "Loading existing SSH configuration..."
    source "$HOME/.azurephotoflow-ssh.env"
fi

print_header "📋 Required Pipeline Variables"
echo ""
echo "The following variables need to be set in your Azure DevOps pipeline:"
echo ""

# Generate the pipeline variables
cat > pipeline-variables.txt << EOF
# Azure DevOps Pipeline Variables for Remote Deployment
# Add these to your variable group 'PhotoFlow' in Azure DevOps

# Remote Server Configuration
REMOTE_SSH_HOST=${SSH_HOST:-"YOUR_SERVER_IP"}
REMOTE_SSH_USER=${SSH_USER:-"YOUR_USERNAME"}
REMOTE_SSH_PORT=${SSH_PORT:-22}

# Production Domain
PRODUCTION_DOMAIN=yourdomain.com

# SSH Private Key (for pipeline authentication)
# Copy the content of ~/.ssh/azurephotoflow-k8s
REMOTE_SSH_PRIVATE_KEY=[PASTE_PRIVATE_KEY_CONTENT_HERE]

# Container Registry (already configured)
GHCR_USERNAME=your-github-username
GHCR_TOKEN=your-github-token

# Application Secrets (already configured)
VITE_GOOGLE_CLIENT_ID=your-google-client-id
JWT_SECRET_KEY=your-jwt-secret
MINIO_ACCESS_KEY=minioadmin
MINIO_SECRET_KEY=minioadmin

# Build Configuration (already configured)
MODE=production
VITE_API_BASE_URL=https://yourdomain.com/api
ALLOWED_ORIGINS=https://yourdomain.com
QDRANT_URL=http://qdrant-service:6333
QDRANT_COLLECTION=images
CLIP_MODEL_PATH=/models/model.onnx
EOF

print_success "Pipeline variables template created: pipeline-variables.txt"
echo ""

print_header "🔑 SSH Private Key Content"
echo ""
print_info "Copy the following SSH private key content to the REMOTE_SSH_PRIVATE_KEY variable:"
echo ""
echo "----------------------------------------"
cat "$SSH_KEY_PATH"
echo "----------------------------------------"
echo ""

print_header "📋 Azure DevOps Setup Instructions"
echo ""
echo "1. 🌐 Go to your Azure DevOps project"
echo "2. 📁 Navigate to Pipelines > Library"
echo "3. ➕ Edit your 'PhotoFlow' variable group"
echo "4. 🔧 Add these new variables:"
echo ""

# Show the key variables that need to be added
echo "   Required New Variables:"
echo "   ----------------------"
echo "   REMOTE_SSH_HOST         = ${SSH_HOST:-YOUR_SERVER_IP}"
echo "   REMOTE_SSH_USER         = ${SSH_USER:-YOUR_USERNAME}"
echo "   REMOTE_SSH_PORT         = ${SSH_PORT:-22}"
echo "   PRODUCTION_DOMAIN       = yourdomain.com"
echo "   REMOTE_SSH_PRIVATE_KEY  = [PRIVATE_KEY_CONTENT_FROM_ABOVE]"
echo ""

echo "5. 🔒 Make sure to mark REMOTE_SSH_PRIVATE_KEY as 'Keep this value secret'"
echo "6. 💾 Save the variable group"
echo ""

print_header "🏗️ Azure DevOps Environment Setup"
echo ""
echo "Create these environments in Azure DevOps (Pipelines > Environments):"
echo ""
echo "1. 📋 production-approval"
echo "   - Add manual approval gates"
echo "   - Assign approvers (yourself and/or team members)"
echo "   - Set approval timeout (e.g., 24 hours)"
echo ""
echo "2. 🚀 production-deployment"
echo "   - This environment will track deployment history"
echo "   - Optionally add additional checks/gates"
echo ""

print_header "🔄 Pipeline Usage"
echo ""
echo "Once configured, your enhanced pipeline will:"
echo ""
echo "✅ 1. Build and push Docker images (automatic)"
echo "✅ 2. Run tests (automatic)"
echo "🤚 3. Wait for deployment approval (manual)"
echo "🚀 4. Deploy to remote cluster (automatic after approval)"
echo "📊 5. Verify deployment and send notifications (automatic)"
echo ""

print_info "To trigger the pipeline:"
echo "  - Push to main branch"
echo "  - Wait for build and test stages"
echo "  - Approve deployment when prompted"
echo "  - Monitor deployment progress"
echo ""

print_header "🔧 Testing the Setup"
echo ""
echo "Before using the pipeline, test your configuration:"
echo ""
echo "1. Test SSH connection:"
echo "   ssh -i $SSH_KEY_PATH ${SSH_USER:-USER}@${SSH_HOST:-HOST}"
echo ""
echo "2. Test MicroK8s access:"
echo "   ssh -i $SSH_KEY_PATH ${SSH_USER:-USER}@${SSH_HOST:-HOST} 'microk8s status'"
echo ""
echo "3. Test deployment script locally:"
echo "   ./deploy-remote.sh"
echo ""

print_header "📁 File Updates"
echo ""
echo "To use the enhanced pipeline:"
echo ""
echo "1. Replace your current azure-pipelines.yml:"
echo "   mv azure-pipelines.yml azure-pipelines-old.yml"
echo "   mv azure-pipelines-enhanced.yml azure-pipelines.yml"
echo ""
echo "2. Commit and push the changes:"
echo "   git add ."
echo "   git commit -m 'Add enhanced CI/CD pipeline with remote deployment'"
echo "   git push origin main"
echo ""

print_success "Setup complete! Review the instructions above to configure your pipeline."
echo ""
print_info "Full setup instructions are in: pipeline-variables.txt"