# 🚀 Enhanced CI/CD Pipeline with Remote Deployment

This guide shows how to integrate remote deployment into your CI/CD pipeline with manual approval gates, so you only need to run the pipeline and decide whether to deploy.

## 🎯 Overview

The enhanced pipeline provides a complete **Build → Test → Approve → Deploy** workflow:

```mermaid
graph LR
    A[Push to Main] --> B[Build Images]
    B --> C[Run Tests]
    C --> D{Deployment Configured?}
    D -->|Yes| E[Wait for Approval]
    D -->|No| F[Skip Deployment]
    E -->|Approved| G[Deploy to Remote]
    E -->|Rejected| H[Stop]
    G --> I[Verify & Notify]
```

## ✨ Key Features

- **🤖 Automated Build & Test**: Images built and tests run automatically
- **🤚 Manual Approval**: Deploy only when you approve it
- **🚀 Remote Deployment**: Deploys directly to your MicroK8s cluster
- **✅ Verification**: Automatic health checks and status reporting
- **📢 Notifications**: Status updates and access information
- **🔄 Rollback Ready**: Easy to revert if needed

## 🛠️ Setup Instructions

### 1. Configure Pipeline Variables

Run the setup script to get your configuration:

```bash
./setup-pipeline-deployment.sh
```

This generates the exact variables you need to add to Azure DevOps.

### 2. Azure DevOps Configuration

#### **A. Update Variable Group**

In Azure DevOps:
1. Go to **Pipelines > Library**
2. Edit your **'PhotoFlow'** variable group
3. Add these new variables:

```yaml
# Remote Server Configuration
REMOTE_SSH_HOST: "your-server-ip"
REMOTE_SSH_USER: "your-username"  
REMOTE_SSH_PORT: "22"
PRODUCTION_DOMAIN: "yourdomain.com"

# SSH Authentication (mark as secret)
REMOTE_SSH_PRIVATE_KEY: "[your-ssh-private-key-content]"

# Container Registry (if not already set)
GHCR_USERNAME: "your-github-username"
GHCR_TOKEN: "[your-github-token]"  # Mark as secret

# Application Configuration
VITE_GOOGLE_CLIENT_ID: "[your-google-client-id]"  # Mark as secret
JWT_SECRET_KEY: "[your-jwt-secret]"  # Mark as secret
MINIO_ACCESS_KEY: "minioadmin"
MINIO_SECRET_KEY: "minioadmin"  # Mark as secret
```

#### **B. Create Environments**

Create these environments in **Pipelines > Environments**:

1. **`production-approval`**
   - Add **manual approval gates**
   - Assign approvers (yourself and/or team members)
   - Set approval timeout (e.g., 24 hours)

2. **`production-deployment`**
   - Tracks deployment history
   - Optional: Add additional security checks

### 3. Update Pipeline File

Replace your current pipeline:

```bash
# Backup current pipeline
mv azure-pipelines.yml azure-pipelines-old.yml

# Use enhanced pipeline
mv azure-pipelines-enhanced.yml azure-pipelines.yml

# Commit changes
git add .
git commit -m "Add enhanced CI/CD pipeline with remote deployment"
git push origin main
```

## 🔄 How It Works

### **Stage 1: Build** (Automatic)
- Downloads CLIP model
- Builds Docker images
- Pushes to GitHub Container Registry
- Publishes deployment artifacts

### **Stage 2: Test** (Automatic)  
- Runs backend unit tests
- Runs frontend tests
- Publishes test results

### **Stage 3: Deployment Approval** (Manual)
- Checks if deployment is configured
- Creates approval gate
- Waits for manual approval

### **Stage 4: Remote Deploy** (Automatic after approval)
- Sets up SSH connection to your server
- Updates Kubernetes manifests with build info
- Creates/updates secrets on remote cluster
- Deploys application components
- Waits for deployments to be ready
- Verifies deployment health

### **Stage 5: Notification** (Automatic)
- Reports deployment status
- Provides access URLs
- Shows deployment summary

## 🎮 Using the Pipeline

### **Normal Workflow**

1. **Push to main branch**:
   ```bash
   git push origin main
   ```

2. **Monitor build and test** (automatic)
   - Watch pipeline progress in Azure DevOps
   - Build and test stages run automatically

3. **Approve deployment** (manual)
   - Get notification when approval is needed
   - Review changes and approve/reject deployment
   - Pipeline waits for your decision

4. **Deployment runs** (automatic after approval)
   - Application deploys to your cluster
   - Health checks verify everything works
   - Get notification with access URLs

### **Skipping Deployment**

- **Reject approval**: Pipeline stops, no deployment
- **No approval**: Pipeline times out and stops
- **Missing config**: Pipeline skips deployment stage

## 📊 Pipeline Outputs

### **Successful Deployment**
```
🎉 Deployment completed successfully!

Deployment Summary:
  Build ID: 12345
  Target: user@10.0.0.2
  Domain: myapp.example.com
  Status: ✅ SUCCESS

Access Information:
  Frontend: https://myapp.example.com
  API: https://myapp.example.com/api
  Health: https://myapp.example.com/api/health
```

### **Failed Deployment**
```
❌ Deployment failed!

Error: Backend deployment timeout
Check logs: kubectl logs deployment/backend-deployment -n azurephotoflow
```

## 🔧 Advanced Configuration

### **Custom Approval Rules**

In the `production-approval` environment:

```yaml
# Example: Require multiple approvers
approvals:
  - type: manual
    required_approvers: 2
    timeout: 1440  # 24 hours
    approvers:
      - user1@company.com
      - user2@company.com
```

### **Branch-based Deployment**

```yaml
# Only deploy from specific branches
condition: and(
  succeeded(), 
  in(variables['Build.SourceBranch'], 'refs/heads/main', 'refs/heads/release/*')
)
```

### **Environment-specific Deployment**

```yaml
# Deploy to different clusters based on branch
- ${{ if eq(variables['Build.SourceBranch'], 'refs/heads/develop') }}:
  - template: deploy-staging.yml
- ${{ if eq(variables['Build.SourceBranch'], 'refs/heads/main') }}:
  - template: deploy-production.yml
```

## 🚨 Troubleshooting

### **Common Issues**

#### **SSH Connection Failed**
```bash
# Test SSH manually
ssh -i ~/.ssh/azurephotoflow-k8s user@server

# Check pipeline variable
echo $REMOTE_SSH_PRIVATE_KEY | head -1
# Should show: -----BEGIN OPENSSH PRIVATE KEY-----
```

#### **MicroK8s Not Ready**
```bash
# Check cluster status
ssh user@server 'microk8s status'

# Enable required addons
ssh user@server 'microk8s enable dns storage ingress cert-manager'
```

#### **Deployment Timeout**
```bash
# Check pod status
ssh user@server 'microk8s kubectl get pods -n azurephotoflow'

# Check events
ssh user@server 'microk8s kubectl get events -n azurephotoflow'
```

#### **Approval Not Showing**
- Check if `production-approval` environment exists
- Verify you're added as an approver
- Check notification settings in Azure DevOps

### **Debug Commands**

```bash
# Test pipeline configuration
./setup-pipeline-deployment.sh

# Test remote deployment manually
./deploy-remote.sh

# Check pipeline variables
az pipelines variable-group list --group-name PhotoFlow
```

## 🎯 Benefits

### **For Developers**
- **One command deployment**: Just push to main
- **Safety net**: Manual approval prevents accidental deploys
- **Clear feedback**: Know exactly what's happening
- **Easy rollback**: Previous images tagged and ready

### **For Operations**
- **Audit trail**: All deployments tracked in Azure DevOps
- **Consistent process**: Same steps every time
- **Automated verification**: Health checks ensure deployment success
- **Notification integration**: Teams/Slack/email notifications

### **For Teams**
- **Collaboration**: Multiple approvers for critical deployments
- **Visibility**: Everyone can see deployment status
- **Documentation**: Pipeline serves as deployment documentation
- **Compliance**: Approval process meets governance requirements

## 🔄 Rollback Process

If something goes wrong:

```bash
# Option 1: Redeploy previous version via pipeline
# Find previous successful build ID, then:
# Set FORCE_DEPLOY_BUILD_ID variable and rerun pipeline

# Option 2: Manual rollback
ssh user@server
microk8s kubectl rollout undo deployment/backend-deployment -n azurephotoflow
microk8s kubectl rollout undo deployment/frontend-deployment -n azurephotoflow

# Option 3: Deploy specific version
./scripts/deploy-k8s-remote.sh production v1.2.3
```

## 📈 Next Steps

Once your enhanced pipeline is working:

1. **Add monitoring**: Integrate with Prometheus/Grafana
2. **Add notifications**: Connect to Slack/Teams webhooks
3. **Add security scans**: Include container security scanning
4. **Add performance tests**: Run load tests after deployment
5. **Add blue-green deployment**: Zero-downtime deployments

---

This enhanced pipeline transforms your deployment process from manual script execution to a professional, automated, and controlled CI/CD workflow! 🚀