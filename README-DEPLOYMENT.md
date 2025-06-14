# 🚀 Simple Remote Deployment Guide

## One Script Does Everything

```bash
# Deploy AzurePhotoFlow to your remote MicroK8s cluster
./deploy-remote.sh
```

That's it! This single script will guide you through the entire process step by step.

## 📋 What You Need Before Starting

1. **Remote server** with MicroK8s installed
2. **SSH access** to that server  
3. **Domain name** pointing to your server
4. **Google OAuth Client ID** (get from [Google Cloud Console](https://console.cloud.google.com/))
5. **GitHub Personal Access Token** (for Docker images)

## 🎯 What the Script Does

The `deploy-remote.sh` script handles everything automatically:

### ✅ **Step 1: SSH Setup**
- Tests your SSH connection
- Sets up SSH keys if needed (eliminates password prompts)
- Creates environment file for future use

### ✅ **Step 2: Cluster Preparation** 
- Checks MicroK8s status and addons
- Enables required addons (dns, storage, ingress, cert-manager)
- Validates cluster readiness

### ✅ **Step 3: Application Configuration**
- Collects your domain name and OAuth details
- Updates configuration files with your settings
- Sets up secrets on the remote cluster

### ✅ **Step 4: Application Deployment**
- Deploys storage components (MinIO, Qdrant)
- Deploys application (backend, frontend)
- Configures ingress and SSL certificates

### ✅ **Step 5: Verification**
- Runs health checks
- Shows deployment status
- Provides access information

### ✅ **Step 6: Next Steps**
- DNS configuration instructions
- SSL certificate status
- Monitoring and troubleshooting commands

## 🔧 Manual Commands (If Needed)

If you prefer to run individual steps:

```bash
# 1. Setup SSH (one-time)
./scripts/fix-ssh-auth.sh
source ~/.azurephotoflow-ssh.env

# 2. Prepare cluster
./scripts/prepare-microk8s-remote.sh

# 3. Setup secrets
./scripts/setup-secrets-remote.sh

# 4. Deploy application
./scripts/deploy-k8s-remote.sh production latest

# 5. Monitor
./scripts/monitor-k8s-remote.sh
```

## 🚨 Quick Troubleshooting

### SSH Issues
```bash
# If SSH fails, run the fix script
./scripts/fix-ssh-auth.sh
```

### MicroK8s Issues  
```bash
# SSH to server and enable addons
ssh user@server
microk8s enable dns storage ingress cert-manager
```

### Deployment Issues
```bash
# Check status
./scripts/monitor-k8s-remote.sh health

# View logs
./scripts/monitor-k8s-remote.sh logs backend-deployment-xxx
```

### Access Issues
```bash
# Port forward for testing
ssh -L 8080:localhost:80 user@server 'microk8s kubectl port-forward service/frontend-service 80:80 -n azurephotoflow'
# Then visit http://localhost:8080
```

## 🎯 After Deployment

1. **Point your domain** to your server IP
2. **Wait for SSL certificates** (automatic via Let's Encrypt)
3. **Access your app** at `https://yourdomain.com`
4. **Monitor** with `./scripts/monitor-k8s-remote.sh`

## 🔄 Updating Your Deployment

```bash
# Redeploy with new image version
./scripts/deploy-k8s-remote.sh production v1.2.3

# Or re-run the full setup
./deploy-remote.sh
```

## 📊 Monitoring

```bash
# Overall status
./scripts/monitor-k8s-remote.sh

# Health checks  
./scripts/monitor-k8s-remote.sh health

# View logs
./scripts/monitor-k8s-remote.sh logs pod-name

# Watch pods real-time
./scripts/monitor-k8s-remote.sh watch
```

---

## 🎉 That's It!

The deployment process has been simplified to a single command that guides you through everything. No more confusion about which script to run when!

**Just run: `./deploy-remote.sh`**