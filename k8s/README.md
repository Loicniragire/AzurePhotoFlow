# Kubernetes Deployment Strategy for AzurePhotoFlow

This directory contains all Kubernetes manifests and deployment configurations for the AzurePhotoFlow application.

## Architecture Overview

The application consists of:
- **Frontend**: React application (nginx-served)
- **Backend API**: ASP.NET Core Web API
- **MinIO**: S3-compatible object storage
- **Qdrant**: Vector database for embeddings
- **NGINX Ingress**: Load balancer and SSL termination

## Prerequisites

1. Kubernetes cluster (1.20+)
2. NGINX Ingress Controller installed
3. kubectl configured to access your cluster
4. Persistent volumes available for storage

## Quick Start

1. **Create namespace:**
   ```bash
   kubectl apply -f namespace.yaml
   ```

2. **Create secrets:**
   ```bash
   kubectl apply -f secrets.yaml
   ```

3. **Deploy storage components:**
   ```bash
   kubectl apply -f storage/
   ```

4. **Deploy application:**
   ```bash
   kubectl apply -f app/
   ```

5. **Configure ingress:**
   ```bash
   kubectl apply -f ingress.yaml
   ```

## Directory Structure

```
k8s/
├── README.md                 # This file
├── namespace.yaml           # Namespace definition
├── secrets.yaml             # Application secrets
├── configmap.yaml           # Configuration data
├── storage/
│   ├── minio-pvc.yaml      # MinIO persistent storage
│   ├── minio-deployment.yaml
│   ├── minio-service.yaml
│   ├── qdrant-pvc.yaml     # Qdrant persistent storage
│   ├── qdrant-deployment.yaml
│   └── qdrant-service.yaml
├── app/
│   ├── backend-deployment.yaml
│   ├── backend-service.yaml
│   ├── frontend-deployment.yaml
│   ├── frontend-service.yaml
│   └── hpa.yaml            # Horizontal Pod Autoscaler
└── ingress.yaml            # NGINX Ingress configuration
```

## Configuration

### Environment Variables

Update `secrets.yaml` with your environment-specific values:

- **VITE_GOOGLE_CLIENT_ID**: Google OAuth client ID
- **JWT_SECRET_KEY**: JWT signing secret
- **QDRANT_URL**: Qdrant service URL
- **MINIO_ACCESS_KEY**: MinIO access credentials
- **MINIO_SECRET_KEY**: MinIO secret credentials

### Storage

- **MinIO**: Requires persistent storage for object data
- **Qdrant**: Requires persistent storage for vector data
- Both use PersistentVolumeClaims that need to be backed by your storage class

### Ingress

Configure your domain and SSL certificates in `ingress.yaml`.

## Deployment Commands

```bash
# Deploy everything at once
kubectl apply -f k8s/

# Or deploy step by step
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/storage/
kubectl apply -f k8s/app/
kubectl apply -f k8s/ingress.yaml

# Check deployment status
kubectl get pods -n azurephotoflow
kubectl get services -n azurephotoflow
kubectl get ingress -n azurephotoflow
```

## Scaling

The application supports horizontal scaling:

```bash
# Scale backend
kubectl scale deployment backend-deployment -n azurephotoflow --replicas=3

# Scale frontend
kubectl scale deployment frontend-deployment -n azurephotoflow --replicas=2
```

## Monitoring

```bash
# View logs
kubectl logs -f deployment/backend-deployment -n azurephotoflow
kubectl logs -f deployment/frontend-deployment -n azurephotoflow

# Check resource usage
kubectl top pods -n azurephotoflow
kubectl top nodes
```

## Troubleshooting

Common issues and solutions:

1. **Pods in Pending state**: Check PVC status and available storage
2. **ImagePullBackOff**: Verify image names and registry access
3. **CrashLoopBackOff**: Check logs and environment variables
4. **Service not accessible**: Verify service selector labels match deployment labels

## Security Considerations

- Secrets are base64 encoded but consider using external secret management
- Use NetworkPolicies for micro-segmentation if needed
- Configure RBAC for service accounts
- Enable pod security standards