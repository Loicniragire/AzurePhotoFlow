# Kubernetes Deployment Guide for AzurePhotoFlow

This guide walks you through deploying AzurePhotoFlow to your self-hosted Kubernetes cluster.

## 🎯 Overview

Your deployment strategy includes:
- **CI/CD Pipeline**: Automated building and testing with Azure DevOps
- **Container Registry**: GitHub Container Registry (GHCR) for image storage
- **Kubernetes Deployment**: Self-hosted cluster with NGINX Ingress
- **Storage**: Persistent volumes for MinIO and Qdrant
- **Scaling**: Horizontal Pod Autoscaling for dynamic load handling

## 📋 Prerequisites

### Infrastructure Requirements
- Kubernetes cluster (1.20+) with at least:
  - 3 nodes (recommended)
  - 8GB RAM total
  - 100GB+ storage
- NGINX Ingress Controller installed
- Storage class configured for persistent volumes
- DNS management for your domain

### Tools Required
- `kubectl` configured for your cluster
- Access to your domain's DNS settings
- GitHub Personal Access Token with registry permissions

### Optional but Recommended
- Cert-manager for automatic SSL certificates
- Metrics server for resource monitoring
- Prometheus/Grafana for application monitoring

## 🚀 Deployment Steps

### Step 1: Prepare Your Environment

1. **Clone and navigate to the repository:**
   ```bash
   git clone https://github.com/Loicniragire/AzurePhotoFlow.git
   cd AzurePhotoFlow
   ```

2. **Verify cluster connectivity:**
   ```bash
   kubectl cluster-info
   kubectl get nodes
   ```

### Step 2: Configure Secrets

1. **Run the secrets setup script:**
   ```bash
   ./scripts/setup-secrets.sh
   ```

2. **Or manually create secrets:**
   ```bash
   # Application secrets
   kubectl create secret generic azurephotoflow-secrets \
     --from-literal=VITE_GOOGLE_CLIENT_ID="your-google-client-id" \
     --from-literal=JWT_SECRET_KEY="your-jwt-secret" \
     --from-literal=MINIO_ACCESS_KEY="minioadmin" \
     --from-literal=MINIO_SECRET_KEY="minioadmin" \
     --namespace=azurephotoflow

   # Docker registry secret
   kubectl create secret docker-registry registry-secret \
     --docker-server=ghcr.io \
     --docker-username="your-github-username" \
     --docker-password="your-github-token" \
     --namespace=azurephotoflow
   ```

### Step 3: Configure Environment

1. **Update configuration files:**
   ```bash
   # Edit k8s/configmap.yaml
   vim k8s/configmap.yaml
   ```
   
   Update these values:
   - `VITE_API_BASE_URL`: Your API domain (e.g., `https://api.yourdomain.com`)
   - `ALLOWED_ORIGINS`: Your frontend domain (e.g., `https://yourdomain.com`)

2. **Update ingress configuration:**
   ```bash
   # Edit k8s/ingress.yaml
   vim k8s/ingress.yaml
   ```
   
   Replace `your-domain.com` with your actual domain.

### Step 4: Deploy Application

1. **Quick deployment (all at once):**
   ```bash
   ./scripts/deploy-k8s.sh production latest
   ```

2. **Or step-by-step deployment:**
   ```bash
   # Create namespace
   kubectl apply -f k8s/namespace.yaml

   # Apply secrets and config
   kubectl apply -f k8s/secrets.yaml
   kubectl apply -f k8s/configmap.yaml

   # Deploy storage
   kubectl apply -f k8s/storage/

   # Wait for storage to be ready
   kubectl wait --for=condition=available --timeout=300s deployment/minio-deployment -n azurephotoflow
   kubectl wait --for=condition=available --timeout=300s deployment/qdrant-deployment -n azurephotoflow

   # Deploy application
   kubectl apply -f k8s/app/

   # Deploy ingress
   kubectl apply -f k8s/ingress.yaml
   ```

### Step 5: Configure DNS

1. **Get the ingress external IP:**
   ```bash
   kubectl get ingress azurephotoflow-ingress -n azurephotoflow
   ```

2. **Update your DNS records:**
   - Create A records pointing to the ingress IP:
     - `yourdomain.com` → Ingress IP
     - `api.yourdomain.com` → Ingress IP (if using subdomain)

### Step 6: Verify Deployment

1. **Check deployment status:**
   ```bash
   ./scripts/monitor-k8s.sh
   ```

2. **Test application endpoints:**
   ```bash
   curl https://yourdomain.com
   curl https://api.yourdomain.com/health
   ```

## 🔄 CI/CD Pipeline Setup

### Azure DevOps Configuration

1. **Create Kubernetes Service Connection:**
   - Go to Azure DevOps → Project Settings → Service Connections
   - Create "Kubernetes" connection
   - Configure with your cluster credentials

2. **Update Pipeline Variables:**
   In your Azure DevOps variable group, ensure you have:
   - `VITE_GOOGLE_CLIENT_ID`
   - `JWT_SECRET_KEY`
   - `VITE_API_BASE_URL`
   - `ALLOWED_ORIGINS`
   - `GHCR_USERNAME`
   - `GHCR_TOKEN`

3. **Update Pipeline:**
   In `azure-pipelines.yml`, replace:
   ```yaml
   kubernetesServiceEndpoint: 'your-k8s-connection'
   ```
   With your actual service connection name.

### Pipeline Flow

1. **Build Stage**: Builds Docker images and pushes to GHCR
2. **Test Stage**: Runs backend and frontend tests
3. **Deploy Stage**: Deploys to Kubernetes cluster (only on main branch)

## 📊 Monitoring and Operations

### Daily Operations

```bash
# Monitor cluster health
./scripts/monitor-k8s.sh

# View application logs
kubectl logs -f deployment/backend-deployment -n azurephotoflow
kubectl logs -f deployment/frontend-deployment -n azurephotoflow

# Scale applications
kubectl scale deployment backend-deployment --replicas=3 -n azurephotoflow
```

### Performance Tuning

1. **Horizontal Pod Autoscaling**: Already configured in `k8s/app/hpa.yaml`
2. **Resource Limits**: Adjust in deployment files based on your needs
3. **Persistent Volume Sizes**: Modify in PVC files before deployment

### Backup Strategy

```bash
# Backup MinIO data (schedule this)
kubectl exec -it deployment/minio-deployment -n azurephotoflow -- mc mirror /data /backup

# Backup Qdrant data
kubectl exec -it deployment/qdrant-deployment -n azurephotoflow -- tar -czf /backup/qdrant.tar.gz /qdrant/storage
```

## 🔧 Troubleshooting

### Common Issues

1. **Pods in Pending State:**
   ```bash
   kubectl describe pod <pod-name> -n azurephotoflow
   # Check: PVC binding, resource availability, node selectors
   ```

2. **ImagePullBackOff:**
   ```bash
   kubectl describe pod <pod-name> -n azurephotoflow
   # Check: registry secret, image name, network connectivity
   ```

3. **Application Not Accessible:**
   ```bash
   # Check ingress
   kubectl describe ingress azurephotoflow-ingress -n azurephotoflow
   
   # Test services directly
   kubectl port-forward service/frontend-service 8080:80 -n azurephotoflow
   ```

### Getting Help

```bash
# View all resources
kubectl get all -n azurephotoflow

# Check events
kubectl get events -n azurephotoflow --sort-by=.metadata.creationTimestamp

# Debug specific pod
kubectl describe pod <pod-name> -n azurephotoflow
kubectl logs <pod-name> -n azurephotoflow
```

## 🔒 Security Considerations

1. **Network Policies**: Consider implementing for micro-segmentation
2. **RBAC**: Configure role-based access control
3. **Pod Security Standards**: Enable admission controllers
4. **Secrets Management**: Consider external secret management (e.g., HashiCorp Vault)
5. **Image Security**: Scan images for vulnerabilities

## 📈 Scaling Guidelines

- **Backend**: Can scale horizontally (2-10 replicas)
- **Frontend**: Can scale horizontally (2-5 replicas)
- **MinIO**: Single instance (for data consistency)
- **Qdrant**: Single instance (for this deployment)

For high availability, consider:
- Multiple MinIO instances with distributed setup
- Qdrant clustering (enterprise feature)
- Multi-region deployment