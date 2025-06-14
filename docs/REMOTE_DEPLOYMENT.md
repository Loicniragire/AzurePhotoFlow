# 🌐 Remote MicroK8s Deployment Guide

This guide covers deploying AzurePhotoFlow to a **remote MicroK8s cluster** that you access via SSH. This is ideal for:

- Dedicated servers on your local network
- Cloud VMs running MicroK8s
- Raspberry Pi clusters
- Development environments on separate machines

## 📋 Prerequisites

### Local Machine Requirements
- SSH client installed
- SSH access to the remote server
- Network connectivity to the remote server

### Remote Server Requirements
- Ubuntu/Debian-based Linux distribution
- MicroK8s installed and running
- SSH server configured
- User account with sudo privileges (for initial setup)

## 🚀 Quick Start

### 1. Test SSH Connection

First, ensure you can connect to your remote server:

```bash
# Test basic SSH connectivity
ssh username@server-ip-address

# Or with SSH key
ssh -i ~/.ssh/your-key username@server-ip-address
```

### 2. Prepare Remote Cluster

Run the remote preparation script to validate your cluster:

```bash
# Using command line options
./scripts/prepare-microk8s-remote.sh -h 192.168.1.100 -u ubuntu

# Using environment variables
export SSH_HOST=192.168.1.100
export SSH_USER=ubuntu
export SSH_KEY=~/.ssh/microk8s-key  # Optional
./scripts/prepare-microk8s-remote.sh
```

### 3. Setup Application Secrets

Configure authentication and API keys:

```bash
# Interactive setup
./scripts/setup-secrets-remote.sh -h 192.168.1.100 -u ubuntu
```

### 4. Deploy Application

Deploy AzurePhotoFlow to the remote cluster:

```bash
# Deploy with latest images
./scripts/deploy-k8s-remote.sh production latest -h 192.168.1.100 -u ubuntu

# Deploy specific version
./scripts/deploy-k8s-remote.sh production v1.2.3 -h 192.168.1.100 -u ubuntu
```

### 5. Monitor Deployment

Monitor the deployment status:

```bash
# Overall status
./scripts/monitor-k8s-remote.sh -h 192.168.1.100 -u ubuntu

# Health checks
./scripts/monitor-k8s-remote.sh -h 192.168.1.100 -u ubuntu health

# Watch pods in real-time
./scripts/monitor-k8s-remote.sh -h 192.168.1.100 -u ubuntu watch
```

## 📚 Detailed Instructions

### Remote Server Setup

If MicroK8s is not yet installed on your remote server:

```bash
# SSH into the remote server
ssh username@server-ip

# Install MicroK8s
sudo snap install microk8s --classic

# Add user to microk8s group
sudo usermod -a -G microk8s $USER

# Exit and reconnect SSH session for group changes
exit
ssh username@server-ip

# Verify installation
microk8s status --wait-ready
```

### Environment Variables

You can set these environment variables to avoid typing SSH connection details repeatedly:

```bash
# Add to your ~/.bashrc or ~/.zshrc
export SSH_HOST=192.168.1.100
export SSH_USER=ubuntu
export SSH_PORT=22
export SSH_KEY=~/.ssh/microk8s-key  # Optional

# Then simply run:
./scripts/prepare-microk8s-remote.sh
./scripts/deploy-k8s-remote.sh production latest
```

### SSH Key Authentication

For better security and convenience, use SSH key authentication:

```bash
# Generate SSH key pair (if you don't have one)
ssh-keygen -t rsa -b 4096 -f ~/.ssh/microk8s-key

# Copy public key to remote server
ssh-copy-id -i ~/.ssh/microk8s-key.pub username@server-ip

# Test key-based authentication
ssh -i ~/.ssh/microk8s-key username@server-ip

# Use key with scripts
./scripts/prepare-microk8s-remote.sh -h server-ip -u username -k ~/.ssh/microk8s-key
```

## 🛠️ Script Reference

### prepare-microk8s-remote.sh

Validates and prepares the remote MicroK8s cluster:

```bash
# Basic usage
./scripts/prepare-microk8s-remote.sh -h HOST -u USER

# With SSH key
./scripts/prepare-microk8s-remote.sh -h HOST -u USER -k SSH_KEY

# Custom SSH port
./scripts/prepare-microk8s-remote.sh -h HOST -u USER -p 2222
```

**What it checks:**
- SSH connectivity
- MicroK8s installation and status
- Required addons (dns, storage)
- Optional addons (ingress, cert-manager, metrics-server)
- Storage configuration
- Local kubectl configuration for remote access

### setup-secrets-remote.sh

Configures application secrets on the remote cluster:

```bash
./scripts/setup-secrets-remote.sh -h HOST -u USER
```

**Secrets it creates:**
- Google OAuth Client ID
- JWT Secret Key
- MinIO credentials
- Docker registry credentials

### deploy-k8s-remote.sh

Deploys the application to the remote cluster:

```bash
# Basic deployment
./scripts/deploy-k8s-remote.sh production latest -h HOST -u USER

# Arguments:
# 1. Environment (production/development)
# 2. Image tag (latest/v1.2.3/etc)
# 3. SSH connection options
```

**Deployment process:**
1. Tests SSH connectivity
2. Validates MicroK8s status
3. Copies Kubernetes manifests to remote server
4. Creates namespace and applies configurations
5. Deploys storage components (MinIO, Qdrant)
6. Deploys application components (backend, frontend)
7. Configures ingress
8. Provides access information

### monitor-k8s-remote.sh

Monitors and troubleshoots the remote deployment:

```bash
# Status overview
./scripts/monitor-k8s-remote.sh -h HOST -u USER status

# Detailed pod information
./scripts/monitor-k8s-remote.sh -h HOST -u USER pods

# Service and endpoint information
./scripts/monitor-k8s-remote.sh -h HOST -u USER services

# Ingress configuration
./scripts/monitor-k8s-remote.sh -h HOST -u USER ingress

# View logs for specific pod
./scripts/monitor-k8s-remote.sh -h HOST -u USER logs pod-name

# Recent events
./scripts/monitor-k8s-remote.sh -h HOST -u USER events

# Resource usage (requires metrics-server)
./scripts/monitor-k8s-remote.sh -h HOST -u USER resources

# Health checks
./scripts/monitor-k8s-remote.sh -h HOST -u USER health

# Watch pods in real-time
./scripts/monitor-k8s-remote.sh -h HOST -u USER watch
```

## 🔧 Configuration

### Local kubectl Access

The preparation script automatically configures your local kubectl to access the remote cluster:

```bash
# After running prepare-microk8s-remote.sh, you can use kubectl locally:
kubectl get nodes
kubectl get pods -n azurephotoflow

# If this doesn't work, manually configure:
ssh username@server-ip 'microk8s config' > ~/.kube/config
# Edit ~/.kube/config and replace 127.0.0.1 with your server IP
```

### Domain Configuration

Update your domain settings in the configuration files:

```bash
# Edit configmap for API URLs
vim k8s/configmap.yaml

# Edit ingress for domain routing
vim k8s/ingress-microk8s.yaml

# Replace 'your-domain.com' with your actual domain
```

### Network Access

For external access to your application:

**Option 1: MetalLB (Recommended)**
```bash
# Enable MetalLB on remote server with your IP range
ssh username@server-ip 'microk8s enable metallb:192.168.1.240-192.168.1.250'
```

**Option 2: NodePort**
```bash
# Access via node IP and port
# The scripts will show you the access URLs
```

**Option 3: Port Forwarding**
```bash
# For testing, you can port-forward through SSH
ssh -L 8080:localhost:80 username@server-ip 'microk8s kubectl port-forward service/frontend-service 80:80 -n azurephotoflow'
# Then access http://localhost:8080
```

## 🚨 Troubleshooting

### Common Issues

**SSH Connection Failed:**
```bash
# Check SSH service on remote server
ssh username@server-ip 'sudo systemctl status ssh'

# Check firewall
ssh username@server-ip 'sudo ufw status'

# Test different port
./scripts/prepare-microk8s-remote.sh -h HOST -u USER -p 2222
```

**MicroK8s Not Ready:**
```bash
# Check MicroK8s status
ssh username@server-ip 'microk8s status'

# Start MicroK8s
ssh username@server-ip 'microk8s start'

# Check logs
ssh username@server-ip 'microk8s inspect'
```

**Addon Issues:**
```bash
# Enable required addons
ssh username@server-ip 'microk8s enable dns storage ingress'

# Check addon status
ssh username@server-ip 'microk8s status'
```

**Pod Issues:**
```bash
# Check pod status
./scripts/monitor-k8s-remote.sh -h HOST -u USER pods

# View pod logs
./scripts/monitor-k8s-remote.sh -h HOST -u USER logs pod-name

# Check events
./scripts/monitor-k8s-remote.sh -h HOST -u USER events
```

### Debugging Commands

```bash
# SSH into remote server for manual debugging
ssh username@server-ip

# Once connected, use standard kubectl commands:
microk8s kubectl get pods -n azurephotoflow
microk8s kubectl describe pod pod-name -n azurephotoflow
microk8s kubectl logs pod-name -n azurephotoflow
```

## 🔒 Security Considerations

### SSH Security
- Use SSH key authentication instead of passwords
- Consider changing the default SSH port
- Restrict SSH access by IP if possible
- Use SSH tunneling for sensitive operations

### Network Security
- Configure firewall rules appropriately
- Use VPN for remote access if needed
- Consider using Wireguard or similar for secure networking

### Cluster Security
- Regularly update MicroK8s and the underlying OS
- Use RBAC for access control
- Enable audit logging
- Regularly backup your cluster configuration

## 📊 Performance Tips

### Resource Allocation
- Ensure adequate CPU and memory on the remote server
- Monitor resource usage with the monitoring script
- Scale deployments based on load

### Network Optimization
- Use local DNS resolution where possible
- Consider network latency between your machine and the remote server
- Use persistent SSH connections (SSH multiplexing)

### SSH Multiplexing
Add to your `~/.ssh/config`:
```
Host microk8s-server
    HostName 192.168.1.100
    User ubuntu
    IdentityFile ~/.ssh/microk8s-key
    ControlMaster auto
    ControlPath ~/.ssh/control-%r@%h:%p
    ControlPersist 10m
```

Then use:
```bash
./scripts/prepare-microk8s-remote.sh -h microk8s-server -u ubuntu
```

## 🎯 Next Steps

After successful deployment:

1. **Configure DNS**: Point your domain to the cluster IP
2. **Setup SSL**: Ensure cert-manager is issuing certificates
3. **Monitor**: Set up regular health checks
4. **Backup**: Configure backup strategies for persistent data
5. **Scale**: Adjust replica counts based on usage

For production deployments, consider:
- High availability setup with multiple nodes
- External load balancers
- Monitoring and alerting solutions
- Disaster recovery procedures