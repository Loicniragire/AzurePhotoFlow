# 🔍 Cluster Configuration Checker

The `check-cluster-config.py` script is an intelligent MicroK8s cluster analyzer that provides comprehensive assessment of your Kubernetes cluster state and generates smart deployment recommendations.

## 📋 Overview

This tool analyzes your remote MicroK8s cluster and provides:
- ✅ Detailed cluster health assessment
- 🔧 Specific actions needed for deployment readiness
- 💡 Intelligent deployment strategy recommendations
- 📊 JSON output for programmatic consumption

## 🚀 Quick Start

### Basic Usage
```bash
# Check your cluster with command line arguments
python3 scripts/deployment/check-cluster-config.py -h 10.0.0.2 -u loicn

# Using environment variables
export SSH_HOST=10.0.0.2
export SSH_USER=loicn
python3 scripts/check-cluster-config.py
```

### Help Information
```bash
python3 scripts/check-cluster-config.py --help
```

## 📖 Command Line Options

| Option | Description | Example |
|--------|-------------|---------|
| `-h, --host` | Remote server hostname/IP | `-h 192.168.1.100` |
| `-u, --user` | SSH username | `-u ubuntu` |
| `-p, --port` | SSH port (default: 22) | `-p 2222` |
| `-k, --key` | SSH private key path | `-k ~/.ssh/id_rsa` |
| `-o, --output` | Output JSON file | `-o my-config.json` |
| `--help` | Show help message | `--help` |

## 🌍 Environment Variables

You can use environment variables instead of or in combination with command line arguments:

| Variable | Description | Example |
|----------|-------------|---------|
| `SSH_HOST` or `REMOTE_SSH_HOST` | Remote server hostname/IP | `export SSH_HOST=10.0.0.2` |
| `SSH_USER` or `REMOTE_SSH_USER` | SSH username | `export SSH_USER=loicn` |
| `SSH_PORT` | SSH port | `export SSH_PORT=2222` |
| `SSH_KEY` | SSH private key path | `export SSH_KEY=~/.ssh/id_rsa` |
| `CONFIG_OUTPUT_FILE` | Output file path | `export CONFIG_OUTPUT_FILE=cluster.json` |

## 📝 Usage Examples

### 1. Basic Cluster Check
```bash
# Minimal command - check cluster with IP and username
python3 scripts/check-cluster-config.py -h 192.168.1.100 -u ubuntu
```

### 2. Using Custom SSH Key
```bash
# Specify SSH key for authentication
python3 scripts/check-cluster-config.py \
  -h k8s.example.com \
  -u admin \
  -k ~/.ssh/kubernetes_key
```

### 3. Custom Port and Output File
```bash
# Non-standard SSH port with custom output file
python3 scripts/check-cluster-config.py \
  -h 10.0.0.2 \
  -u loicn \
  -p 2222 \
  -o production-cluster-config.json
```

### 4. Environment Variable Usage
```bash
# Set environment variables
export SSH_HOST=192.168.1.100
export SSH_USER=ubuntu
export SSH_KEY=~/.ssh/id_rsa

# Run without arguments
python3 scripts/check-cluster-config.py
```

### 5. Pipeline Integration
```bash
# How it's used in CI/CD pipelines
SSH_HOST=$REMOTE_SSH_HOST \
SSH_USER=$REMOTE_SSH_USER \
SSH_KEY=~/.ssh/azure_pipeline_key \
python3 scripts/check-cluster-config.py
```

## 🔍 What It Checks

### Infrastructure Analysis
- ✅ **SSH Connectivity** - Validates remote access
- ✅ **MicroK8s Installation** - Checks version and availability
- ✅ **Process Health** - Verifies Kubernetes components are running
- ✅ **API Responsiveness** - Tests kubectl client connectivity

### Cluster Configuration
- ✅ **Required Addons** - DNS, storage, ingress
- ✅ **Optional Addons** - cert-manager, metrics-server, registry
- ✅ **Storage Classes** - Default storage configuration
- ✅ **Persistent Volumes** - Storage inventory

### Application State
- ✅ **Namespaces** - Existence and contents
- ✅ **Secrets** - Application and registry credentials
- ✅ **Deployments** - Current application deployments and health
- ✅ **Services** - Service endpoints and configuration
- ✅ **Ingress** - External access configuration

## 📊 Output Examples

### Terminal Output
```
🎯 Target: loicn@10.0.0.2:22
🔑 Using SSH key: ~/.ssh/azure_pipeline_key
📁 Output file: cluster-config.json

🚀 Starting cluster configuration check...
🔍 Checking SSH connectivity...
✅ SSH connection established
🔍 Checking MicroK8s status...
✅ MicroK8s is installed
📊 MicroK8s version: MicroK8s v1.32.3 revision 8148
✅ MicroK8s processes are running
✅ kubectl client is responsive
🔍 Checking MicroK8s addons...
❌ Required addon 'dns' is missing
❌ Required addon 'storage' is missing
✅ Required addon 'ingress' is enabled
ℹ️  Optional addon 'cert-manager' is not enabled
🔍 Checking namespace 'azurephotoflow'...
✅ Namespace 'azurephotoflow' exists
📊 Found: 2 secrets, 4 deployments, 5 services, 1 ingress
✅ Configuration check completed in 12.3s

============================================================
📋 CLUSTER CONFIGURATION SUMMARY
============================================================
⚠️  Cluster needs preparation

🔧 Actions needed:
  - enable_addons:dns,storage
  - create_secret:registry-secret

💡 Deployment recommendations:
  - PARTIAL_DEPLOYMENT: Some deployments missing or not ready
  - CREATE_REGISTRY_SECRET: Registry secret missing

📄 Detailed results saved to: cluster-config.json
💡 Use this file with smart-deploy.sh for intelligent deployment
```

### JSON Output Structure
```json
{
  "cluster_ready": false,
  "namespaces": {
    "azurephotoflow": {
      "exists": true,
      "secrets": ["azurephotoflow-secrets"],
      "deployments": ["backend-deployment", "frontend-deployment"],
      "services": ["backend-service", "frontend-service"],
      "ingress": ["azurephotoflow-ingress"],
      "pvcs": ["minio-pvc", "qdrant-pvc"]
    }
  },
  "secrets": {
    "azurephotoflow-secrets": true,
    "registry-secret": false
  },
  "deployments": {
    "backend-deployment": {
      "exists": true,
      "status": "1/1",
      "ready": true
    },
    "frontend-deployment": {
      "exists": true,
      "status": "1/1", 
      "ready": true
    },
    "minio-deployment": {
      "exists": false,
      "status": "missing",
      "ready": false
    }
  },
  "addons": {
    "dns": "disabled",
    "storage": "disabled", 
    "ingress": "enabled"
  },
  "actions_needed": [
    "enable_addons:dns,storage",
    "create_secret:registry-secret"
  ],
  "recommendations": [
    "PARTIAL_DEPLOYMENT: Some deployments missing or not ready",
    "CREATE_REGISTRY_SECRET: Registry secret missing"
  ]
}
```

## 🔄 Integration with Smart Deployment

The cluster configuration checker works seamlessly with the smart deployment system:

```bash
# 1. Analyze cluster
python3 scripts/check-cluster-config.py -h 10.0.0.2 -u loicn

# 2. Use results for intelligent deployment
./scripts/smart-deploy.sh
```

## 🚨 Exit Codes

- **0** - Cluster is ready for deployment
- **1** - Cluster needs preparation or errors occurred

## 💡 Deployment Recommendations

### FULL_DEPLOYMENT
- Namespace doesn't exist
- No existing deployments found
- **Action**: Clean installation of all components

### UPDATE_DEPLOYMENT  
- All deployments exist and are healthy
- **Action**: Rolling update with new image tags

### PARTIAL_DEPLOYMENT
- Some deployments missing or unhealthy
- **Action**: Deploy missing components, fix issues

## 🔧 Troubleshooting

### Common Issues

**SSH Connection Failed**
```bash
# Check SSH connectivity manually
ssh -o ConnectTimeout=10 user@host 'echo "test"'

# Verify SSH key permissions
chmod 600 ~/.ssh/your_key
```

**MicroK8s Not Responsive**
```bash
# Restart MicroK8s on remote server
ssh user@host 'microk8s stop && sleep 5 && microk8s start'
```

**Permission Denied**
```bash
# Check if user is in microk8s group
ssh user@host 'groups | grep microk8s'

# Add user to microk8s group (if needed)
ssh user@host 'sudo usermod -a -G microk8s $USER'
```

### Debug Mode

For detailed debugging, you can modify the script to increase verbosity:

```python
# Add this near the top of the script for more detailed output
import logging
logging.basicConfig(level=logging.DEBUG)
```

## 🔗 Related Tools

- **smart-deploy.sh** - Uses configuration output for intelligent deployment
- **restart-cluster.sh** - Restarts MicroK8s cluster when issues detected
- **ssh-helper.sh** - SSH connectivity testing and troubleshooting

## 📚 See Also

- [Smart Deployment Guide](smart-deployment.md)
- [Cluster Management](cluster-management.md)
- [Troubleshooting Guide](troubleshooting.md)