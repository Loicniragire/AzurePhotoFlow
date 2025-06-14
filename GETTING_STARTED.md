# 🚀 Getting Started with AzurePhotoFlow on Kubernetes

This guide will walk you through deploying AzurePhotoFlow to your self-hosted Kubernetes cluster from start to finish.

## 📋 Quick Start Checklist

Follow these steps in order:

### Phase 0: SSH Setup (For Remote Clusters)
- [ ] **Step 0**: Setup passwordless SSH access

### Phase 1: Cluster Preparation
- [ ] **Step 1**: Verify cluster prerequisites
- [ ] **Step 2**: Install required components
- [ ] **Step 3**: Validate cluster readiness

### Phase 2: Application Configuration  
- [ ] **Step 4**: Configure secrets
- [ ] **Step 5**: Update application configuration
- [ ] **Step 6**: Configure domain and SSL

### Phase 3: Deployment
- [ ] **Step 7**: Deploy application
- [ ] **Step 8**: Verify deployment
- [ ] **Step 9**: Configure DNS and test

## 🔑 Phase 0: SSH Setup (For Remote Clusters Only)

### Step 0: Setup Passwordless SSH Access

**Skip this step if using a local cluster.**

For remote clusters, eliminate password prompts by setting up SSH key authentication:

```bash
# Setup SSH keys and configure passwordless access
./scripts/setup-ssh-keys.sh -h YOUR_SERVER_IP -u YOUR_USERNAME

# Load SSH environment variables
source ~/.azurephotoflow-ssh.env

# Test SSH connection and MicroK8s access
./scripts/ssh-helper.sh test
```

**Alternative: Manual SSH Setup**
```bash
# Generate SSH key
ssh-keygen -t rsa -b 4096 -f ~/.ssh/azurephotoflow-k8s

# Copy to remote server
ssh-copy-id -i ~/.ssh/azurephotoflow-k8s.pub user@server-ip

# Set environment variables
export SSH_HOST=YOUR_SERVER_IP
export SSH_USER=YOUR_USERNAME
export SSH_KEY=~/.ssh/azurephotoflow-k8s
```

**✅ Completion Criteria**: SSH connection works without password prompts.

---

## 🔧 Phase 1: Cluster Preparation

### Step 1: Verify Prerequisites

**For Local Clusters:**
```bash
# Run the comprehensive cluster check
./scripts/prepare-cluster.sh
```

**For Remote MicroK8s Clusters (SSH):**
```bash
# Check remote cluster via SSH
./scripts/prepare-microk8s-remote.sh -h YOUR_SERVER_IP -u YOUR_USERNAME

# Or set environment variables
export SSH_HOST=YOUR_SERVER_IP
export SSH_USER=YOUR_USERNAME
./scripts/prepare-microk8s-remote.sh
```

This will check:
- ✅ kubectl connectivity
- ✅ Kubernetes version (≥ v1.20)
- ✅ Node readiness
- ✅ Storage configuration
- ✅ NGINX Ingress Controller
- ✅ SSL certificate management (optional)

### Step 2: Install Missing Components

If the preparation script shows missing components, install them:

```bash
# Install all required components automatically
./scripts/install-components.sh all

# Or install individually:
./scripts/install-components.sh nginx-ingress    # Required
./scripts/install-components.sh cert-manager     # Recommended for SSL
./scripts/install-components.sh metrics-server   # Recommended for monitoring
./scripts/install-components.sh storage-class    # Required if no default exists
```

### Step 3: Validate Cluster Readiness

Verify everything is working:

```bash
# Quick verification of all components
./scripts/verify-step.sh all

# Or verify individual components:
./scripts/verify-step.sh ingress    # Check NGINX Ingress
./scripts/verify-step.sh storage    # Check storage classes
./scripts/verify-step.sh pvc        # Test PVC creation
```

**✅ Completion Criteria**: All critical checks pass with green checkmarks.

---

## ⚙️ Phase 2: Application Configuration

### Step 4: Configure Secrets

**For Local Clusters:**
```bash
# Interactive secrets setup
./scripts/setup-secrets.sh
```

**For Remote Clusters:**
```bash
# Setup secrets on remote cluster via SSH
./scripts/setup-secrets-remote.sh -h YOUR_SERVER_IP -u YOUR_USERNAME
```

You'll need to provide:
- **Google OAuth Client ID** (for user authentication)
- **JWT Secret Key** (generate a strong random string)
- **GitHub Container Registry credentials** (username and token)

Alternatively, create secrets manually:

```bash
kubectl create secret generic azurephotoflow-secrets \
  --from-literal=VITE_GOOGLE_CLIENT_ID="your-google-client-id" \
  --from-literal=JWT_SECRET_KEY="your-jwt-secret-key" \
  --from-literal=MINIO_ACCESS_KEY="minioadmin" \
  --from-literal=MINIO_SECRET_KEY="minioadmin" \
  --namespace=azurephotoflow

kubectl create secret docker-registry registry-secret \
  --docker-server=ghcr.io \
  --docker-username="your-github-username" \
  --docker-password="your-github-token" \
  --namespace=azurephotoflow
```

### Step 5: Update Application Configuration

Edit the configuration file with your domain:

```bash
# Edit k8s/configmap.yaml
vim k8s/configmap.yaml
```

Update these key values:
```yaml
data:
  VITE_API_BASE_URL: "https://api.yourdomain.com"  # Your API domain
  ALLOWED_ORIGINS: "https://yourdomain.com"        # Your frontend domain
```

### Step 6: Configure Domain and SSL

Update the ingress configuration:

```bash
# Edit k8s/ingress.yaml
vim k8s/ingress.yaml
```

Replace `your-domain.com` with your actual domain in:
- TLS certificate hosts
- Ingress rules
- Host specifications

**✅ Completion Criteria**: All configuration files updated with your domain and settings.

---

## 🚀 Phase 3: Deployment

### Step 7: Deploy Application

**For Local Clusters:**
```bash
# One-command deployment
./scripts/deploy-k8s.sh production latest
```

**For Remote Clusters:**
```bash
# Deploy to remote cluster via SSH
./scripts/deploy-k8s-remote.sh production latest -h YOUR_SERVER_IP -u YOUR_USERNAME
```

This will:
1. Create namespace
2. Apply secrets and configuration
3. Deploy storage (MinIO, Qdrant)
4. Deploy application (backend, frontend)
5. Configure ingress
6. Wait for all components to be ready

### Step 8: Verify Deployment

**For Local Clusters:**
```bash
# Monitor deployment status
./scripts/monitor-k8s.sh

# Check specific components
kubectl get pods -n azurephotoflow
kubectl get services -n azurephotoflow
kubectl get ingress -n azurephotoflow

# View logs if needed
kubectl logs -f deployment/backend-deployment -n azurephotoflow
```

**For Remote Clusters:**
```bash
# Monitor remote deployment status
./scripts/monitor-k8s-remote.sh -h YOUR_SERVER_IP -u YOUR_USERNAME

# Run health checks
./scripts/monitor-k8s-remote.sh -h YOUR_SERVER_IP -u YOUR_USERNAME health

# View logs
./scripts/monitor-k8s-remote.sh -h YOUR_SERVER_IP -u YOUR_USERNAME logs backend-deployment-xxx
```

### Step 9: Configure DNS and Test

1. **Get the external IP:**
   ```bash
   kubectl get ingress azurephotoflow-ingress -n azurephotoflow
   ```

2. **Update DNS records:**
   Create A records pointing to the ingress IP:
   - `yourdomain.com` → Ingress IP
   - `api.yourdomain.com` → Ingress IP (if using subdomain)

3. **Test the application:**
   ```bash
   # Test frontend
   curl -I https://yourdomain.com
   
   # Test backend health
   curl https://api.yourdomain.com/health
   
   # Or test on same domain with /api prefix
   curl https://yourdomain.com/api/health
   ```

**✅ Completion Criteria**: 
- All pods are `Running`
- Ingress has external IP
- DNS resolves to your application
- HTTPS works (certificates issued)
- Application is accessible via browser

---

## 🎯 Quick Commands Reference

### Daily Operations
```bash
# Monitor cluster health
./scripts/monitor-k8s.sh

# Scale backend for more traffic
kubectl scale deployment backend-deployment --replicas=3 -n azurephotoflow

# View application logs
kubectl logs -f deployment/backend-deployment -n azurephotoflow

# Port-forward for local testing
kubectl port-forward service/frontend-service 8080:80 -n azurephotoflow
```

### Troubleshooting
```bash
# Check pod status
kubectl describe pod <pod-name> -n azurephotoflow

# View recent events
kubectl get events -n azurephotoflow --sort-by=.metadata.creationTimestamp

# Test PVC creation
./scripts/verify-step.sh pvc

# Check ingress controller
./scripts/verify-step.sh ingress
```

### Updates and Maintenance
```bash
# Update to new image version
./scripts/deploy-k8s.sh production v1.2.3

# Restart a deployment
kubectl rollout restart deployment/backend-deployment -n azurephotoflow

# Check rollout status
kubectl rollout status deployment/backend-deployment -n azurephotoflow
```

## 🆘 Common Issues and Solutions

### Issue: Pods stuck in `Pending`
**Cause**: Insufficient resources or PVC binding issues
**Solution**: 
```bash
kubectl describe pod <pod-name> -n azurephotoflow
./scripts/verify-step.sh storage
```

### Issue: `ImagePullBackOff`
**Cause**: Registry authentication or image name issues
**Solution**:
```bash
kubectl describe pod <pod-name> -n azurephotoflow
# Check registry secret is correctly configured
```

### Issue: External IP shows `<pending>`
**Cause**: LoadBalancer not supported or cloud provider not configured
**Solution**:
```bash
# Switch to NodePort
kubectl patch svc ingress-nginx-controller -n ingress-nginx -p '{"spec":{"type":"NodePort"}}'
```

### Issue: SSL certificate not issued
**Cause**: cert-manager or DNS not properly configured
**Solution**:
```bash
kubectl describe certificaterequest -n azurephotoflow
kubectl logs -n cert-manager deployment/cert-manager
```

## 📞 Need Help?

1. **Check the troubleshooting guide**: [docs/CLUSTER_PREPARATION.md](docs/CLUSTER_PREPARATION.md)
2. **Run diagnostic scripts**: Use the verification and monitoring scripts
3. **Check logs**: Use `kubectl logs` and `kubectl describe` for detailed information
4. **Review configurations**: Ensure all domain names and secrets are correctly configured

---

## 🎉 Success!

Once everything is working, you should have:
- ✅ AzurePhotoFlow running on your Kubernetes cluster
- ✅ Automatic SSL certificates
- ✅ Horizontal scaling capabilities
- ✅ Persistent storage for images and data
- ✅ Production-ready configuration

Your application will be available at `https://yourdomain.com` with the API at `https://api.yourdomain.com` (or `https://yourdomain.com/api`).