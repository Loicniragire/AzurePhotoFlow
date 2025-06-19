# Kubernetes Cluster Preparation Guide

This guide will help you prepare your self-hosted Kubernetes cluster for AzurePhotoFlow deployment.

## 🎯 Prerequisites Checklist

Before starting, ensure you have:
- [ ] Kubernetes cluster (v1.20+) with at least 3 nodes
- [ ] kubectl installed and configured
- [ ] Cluster admin access
- [ ] Domain name for your application
- [ ] At least 8GB RAM and 100GB storage available

## 🔍 Quick Cluster Validation

Run our automated cluster preparation script:

```bash
./scripts/setup/prepare-microk8s.sh
```

This script will check all requirements and give you a detailed report.

## 📋 Manual Preparation Steps

If you prefer to set up components manually or need to address specific issues:

### 1. Verify Basic Cluster Health

```bash
# Check cluster connectivity
kubectl cluster-info

# List all nodes
kubectl get nodes -o wide

# Check cluster version
kubectl version --short

# Verify system pods are running
kubectl get pods -n kube-system
```

**✅ Success Criteria:**
- All nodes show `Ready` status
- Cluster version is v1.20 or higher
- All system pods are `Running`

### 2. Install NGINX Ingress Controller

```bash
# Install NGINX Ingress Controller
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.8.2/deploy/static/provider/cloud/deploy.yaml

# Wait for deployment to be ready
kubectl wait --namespace ingress-nginx \
  --for=condition=ready pod \
  --selector=app.kubernetes.io/component=controller \
  --timeout=90s

# Verify installation
kubectl get pods -n ingress-nginx
kubectl get svc -n ingress-nginx
```

**✅ Success Criteria:**
- NGINX controller pod is `Running`
- LoadBalancer service has external IP assigned
- Controller responds to health checks

### 3. Configure Storage Classes

Check existing storage classes:
```bash
kubectl get storageclass
```

If no default storage class exists, create one. Example for local storage:

```yaml
# local-storage-class.yaml
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: local-storage
  annotations:
    storageclass.kubernetes.io/is-default-class: "true"
provisioner: kubernetes.io/no-provisioner
volumeBindingMode: WaitForFirstConsumer
```

Apply it:
```bash
kubectl apply -f local-storage-class.yaml
```

**✅ Success Criteria:**
- At least one storage class exists
- Default storage class is marked (optional but recommended)

### 4. Test Persistent Volume Creation

Create a test PVC to verify storage works:

```yaml
# test-pvc.yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: test-pvc
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 1Gi
```

```bash
# Apply and test
kubectl apply -f test-pvc.yaml

# Check if it binds
kubectl get pvc test-pvc

# Clean up
kubectl delete pvc test-pvc
```

**✅ Success Criteria:**
- PVC reaches `Bound` status
- Persistent volume is created automatically

### 5. Install cert-manager (Optional but Recommended)

For automatic SSL certificate management:

```bash
# Install cert-manager
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.2/cert-manager.yaml

# Wait for cert-manager to be ready
kubectl wait --for=condition=available --timeout=300s deployment/cert-manager -n cert-manager
kubectl wait --for=condition=available --timeout=300s deployment/cert-manager-cainjector -n cert-manager
kubectl wait --for=condition=available --timeout=300s deployment/cert-manager-webhook -n cert-manager

# Verify installation
kubectl get pods -n cert-manager
```

Create a ClusterIssuer for Let's Encrypt:

```yaml
# letsencrypt-issuer.yaml
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: your-email@example.com  # Change this to your email
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
    - http01:
        ingress:
          class: nginx
```

```bash
kubectl apply -f letsencrypt-issuer.yaml
```

**✅ Success Criteria:**
- All cert-manager pods are `Running`
- ClusterIssuer is ready: `kubectl get clusterissuer`

### 6. Install metrics-server (Optional but Recommended)

For resource monitoring and horizontal pod autoscaling:

```bash
# Install metrics-server
kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml

# Wait for deployment
kubectl wait --for=condition=available --timeout=300s deployment/metrics-server -n kube-system

# Test metrics
kubectl top nodes
kubectl top pods -n kube-system
```

**✅ Success Criteria:**
- metrics-server pod is `Running`
- `kubectl top nodes` returns resource usage data

## 🔧 Troubleshooting Common Issues

### Issue: NGINX Ingress Controller Pod Not Starting

**Symptoms:**
- Pod stuck in `Pending` or `CrashLoopBackOff`

**Solutions:**
```bash
# Check pod events
kubectl describe pod -n ingress-nginx -l app.kubernetes.io/component=controller

# Check if ports are available
netstat -tulpn | grep :80
netstat -tulpn | grep :443

# If using NodePort instead of LoadBalancer:
kubectl patch svc ingress-nginx-controller -n ingress-nginx -p '{"spec":{"type":"NodePort"}}'
```

### Issue: Storage Class Not Working

**Symptoms:**
- PVCs stuck in `Pending` state

**Solutions:**
```bash
# Check available storage classes
kubectl get storageclass

# Describe the PVC for more details
kubectl describe pvc <pvc-name>

# Check if you need to create persistent volumes manually
kubectl get pv

# For local storage, you might need to create PVs manually
```

### Issue: cert-manager Installation Fails

**Symptoms:**
- Webhook validation errors
- Pods crash with DNS issues

**Solutions:**
```bash
# Check DNS resolution in cluster
kubectl run test-dns --image=busybox --rm -it -- nslookup kubernetes.default

# Disable resource validation temporarily
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.2/cert-manager.yaml --validate=false

# Check webhook connectivity
kubectl get validatingwebhookconfiguration
```

### Issue: External IP Not Assigned to LoadBalancer

**Symptoms:**
- Service shows `<pending>` for EXTERNAL-IP

**Solutions:**
```bash
# Check if your cluster supports LoadBalancer
kubectl describe svc ingress-nginx-controller -n ingress-nginx

# Alternative: Use NodePort
kubectl patch svc ingress-nginx-controller -n ingress-nginx -p '{"spec":{"type":"NodePort"}}'

# Get node IPs and ports
kubectl get nodes -o wide
kubectl get svc ingress-nginx-controller -n ingress-nginx
```

## ✅ Final Validation

After completing all steps, run the validation script again:

```bash
./scripts/prepare-cluster.sh
```

You should see all green checkmarks (✅) for required components.

## 📝 Validation Checklist

Mark each item as complete:

### Required Components
- [ ] kubectl connectivity works
- [ ] Cluster version ≥ 1.20
- [ ] All nodes are Ready
- [ ] Storage class exists and works
- [ ] NGINX Ingress Controller installed and running
- [ ] LoadBalancer service has external IP (or NodePort configured)
- [ ] Test PVC can be created and bound

### Optional but Recommended
- [ ] cert-manager installed and configured
- [ ] ClusterIssuer created for Let's Encrypt
- [ ] metrics-server installed and working
- [ ] Resource usage metrics available

### Network Access
- [ ] External IP accessible from internet (if using LoadBalancer)
- [ ] DNS configured to point to external IP
- [ ] Firewall allows traffic on ports 80/443

## 🎯 Next Steps

Once your cluster is prepared:

1. **Configure Secrets**: Run `./scripts/setup-secrets.sh`
2. **Update Configuration**: Edit domain names in Kubernetes manifests
3. **Deploy Application**: Run `./scripts/deploy-k8s.sh production latest`

## 📞 Getting Help

If you encounter issues:

1. Check the troubleshooting section above
2. Run `./scripts/monitor-k8s.sh` for cluster status
3. Use `kubectl describe` and `kubectl logs` for debugging
4. Check Kubernetes documentation for component-specific issues