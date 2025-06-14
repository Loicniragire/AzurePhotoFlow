#!/bin/bash

# Kubernetes Cluster Preparation Script for AzurePhotoFlow
# This script validates and prepares your cluster for deployment

set -e

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

print_header() {
    echo ""
    print_status $BLUE "=== $1 ==="
}

print_success() {
    print_status $GREEN "✅ $1"
}

print_warning() {
    print_status $YELLOW "⚠️  $1"
}

print_error() {
    print_status $RED "❌ $1"
}

# Check if command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Function to check kubectl connectivity
check_kubectl() {
    print_header "Checking kubectl connectivity"
    
    if ! command_exists kubectl; then
        print_error "kubectl is not installed"
        echo "Install kubectl: https://kubernetes.io/docs/tasks/tools/"
        return 1
    fi
    
    print_success "kubectl is installed"
    
    if ! kubectl cluster-info >/dev/null 2>&1; then
        print_error "Cannot connect to Kubernetes cluster"
        echo "Configure kubectl to connect to your cluster"
        return 1
    fi
    
    print_success "kubectl can connect to cluster"
    kubectl cluster-info
}

# Function to check cluster version
check_cluster_version() {
    print_header "Checking Kubernetes version"
    
    local server_version=$(kubectl version --output=json | jq -r '.serverVersion.major + "." + .serverVersion.minor' 2>/dev/null || echo "unknown")
    
    if [ "$server_version" = "unknown" ]; then
        print_warning "Could not determine cluster version"
    else
        print_success "Cluster version: v$server_version"
        
        # Check if version is at least 1.20
        local major=$(echo $server_version | cut -d. -f1)
        local minor=$(echo $server_version | cut -d. -f2)
        
        if [ "$major" -ge 1 ] && [ "$minor" -ge 20 ]; then
            print_success "Cluster version meets minimum requirements (v1.20+)"
        else
            print_warning "Cluster version may be too old. Recommended: v1.20+"
        fi
    fi
}

# Function to check cluster resources
check_cluster_resources() {
    print_header "Checking cluster resources"
    
    echo "Nodes:"
    kubectl get nodes -o wide
    echo ""
    
    # Check if we have at least one ready node
    local ready_nodes=$(kubectl get nodes --no-headers | grep -c " Ready")
    if [ "$ready_nodes" -gt 0 ]; then
        print_success "$ready_nodes node(s) ready"
    else
        print_error "No ready nodes found"
        return 1
    fi
    
    # Check resource availability if metrics-server is available
    if kubectl top nodes >/dev/null 2>&1; then
        echo "Resource usage:"
        kubectl top nodes
        print_success "Metrics server is available"
    else
        print_warning "Metrics server not available (optional but recommended)"
    fi
}

# Function to check storage classes
check_storage() {
    print_header "Checking storage configuration"
    
    local storage_classes=$(kubectl get storageclass --no-headers 2>/dev/null | wc -l)
    
    if [ "$storage_classes" -gt 0 ]; then
        print_success "Storage classes available:"
        kubectl get storageclass
        
        # Check for default storage class
        local default_sc=$(kubectl get storageclass -o jsonpath='{.items[?(@.metadata.annotations.storageclass\.kubernetes\.io/is-default-class=="true")].metadata.name}' 2>/dev/null)
        if [ -n "$default_sc" ]; then
            print_success "Default storage class: $default_sc"
        else
            print_warning "No default storage class configured"
            echo "You may need to specify storageClassName in PVC manifests"
        fi
    else
        print_error "No storage classes found"
        echo "Configure storage classes for persistent volumes"
        return 1
    fi
}

# Function to check for NGINX Ingress Controller
check_ingress_controller() {
    print_header "Checking NGINX Ingress Controller"
    
    # Check for ingress-nginx namespace
    if kubectl get namespace ingress-nginx >/dev/null 2>&1; then
        print_success "ingress-nginx namespace exists"
    else
        print_warning "ingress-nginx namespace not found"
    fi
    
    # Check for NGINX Ingress Controller pods
    local nginx_pods=$(kubectl get pods -n ingress-nginx -l app.kubernetes.io/name=ingress-nginx --no-headers 2>/dev/null | wc -l)
    
    if [ "$nginx_pods" -gt 0 ]; then
        print_success "NGINX Ingress Controller pods found:"
        kubectl get pods -n ingress-nginx -l app.kubernetes.io/name=ingress-nginx
        
        # Check if pods are running
        local running_pods=$(kubectl get pods -n ingress-nginx -l app.kubernetes.io/name=ingress-nginx --no-headers | grep -c "Running" || echo "0")
        if [ "$running_pods" -gt 0 ]; then
            print_success "$running_pods NGINX Ingress pod(s) running"
        else
            print_error "NGINX Ingress pods not running"
            return 1
        fi
    else
        print_error "NGINX Ingress Controller not found"
        echo ""
        echo "Install NGINX Ingress Controller:"
        echo "kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.8.2/deploy/static/provider/cloud/deploy.yaml"
        return 1
    fi
    
    # Check for LoadBalancer service
    local lb_service=$(kubectl get service -n ingress-nginx ingress-nginx-controller -o jsonpath='{.spec.type}' 2>/dev/null || echo "")
    if [ "$lb_service" = "LoadBalancer" ]; then
        print_success "LoadBalancer service configured"
        kubectl get service -n ingress-nginx ingress-nginx-controller
    else
        print_warning "LoadBalancer service not found or not configured"
    fi
}

# Function to check optional components
check_optional_components() {
    print_header "Checking optional components"
    
    # Check for cert-manager
    if kubectl get namespace cert-manager >/dev/null 2>&1; then
        print_success "cert-manager namespace exists"
        local cert_manager_pods=$(kubectl get pods -n cert-manager --no-headers | grep -c "Running" || echo "0")
        if [ "$cert_manager_pods" -gt 0 ]; then
            print_success "cert-manager is running ($cert_manager_pods pods)"
        else
            print_warning "cert-manager pods not running"
        fi
    else
        print_warning "cert-manager not installed (optional but recommended for SSL)"
        echo "Install: kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.2/cert-manager.yaml"
    fi
    
    # Check for metrics-server (already checked in resources)
    if kubectl get deployment metrics-server -n kube-system >/dev/null 2>&1; then
        print_success "metrics-server is installed"
    else
        print_warning "metrics-server not installed (optional but recommended)"
        echo "Install: kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml"
    fi
}

# Function to test PVC creation
test_pvc_creation() {
    print_header "Testing PVC creation"
    
    local test_pvc_name="test-pvc-azurephotoflow"
    
    # Create test PVC
    cat <<EOF | kubectl apply -f -
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: $test_pvc_name
  namespace: default
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 1Gi
EOF

    # Wait for PVC to be bound
    echo "Waiting for PVC to be bound..."
    if kubectl wait --for=condition=bound pvc/$test_pvc_name --timeout=60s >/dev/null 2>&1; then
        print_success "PVC creation and binding successful"
        
        # Get storage class used
        local storage_class=$(kubectl get pvc $test_pvc_name -o jsonpath='{.spec.storageClassName}')
        print_success "Storage class used: ${storage_class:-"(default)"}"
    else
        print_error "PVC failed to bind within 60 seconds"
        kubectl describe pvc $test_pvc_name
    fi
    
    # Clean up
    kubectl delete pvc $test_pvc_name >/dev/null 2>&1 || true
}

# Function to show installation commands for missing components
show_installation_commands() {
    print_header "Installation commands for missing components"
    
    echo "NGINX Ingress Controller:"
    echo "kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.8.2/deploy/static/provider/cloud/deploy.yaml"
    echo ""
    
    echo "cert-manager (optional but recommended):"
    echo "kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.2/cert-manager.yaml"
    echo ""
    
    echo "metrics-server (optional but recommended):"
    echo "kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml"
    echo ""
    
    echo "Create ClusterIssuer for Let's Encrypt (after cert-manager is installed):"
    cat << 'EOF'
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: your-email@example.com  # Change this!
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
    - http01:
        ingress:
          class: nginx
EOF
}

# Main execution
main() {
    print_status $BLUE "🚀 AzurePhotoFlow Kubernetes Cluster Preparation"
    print_status $BLUE "=============================================="
    
    local exit_code=0
    
    # Run all checks
    check_kubectl || exit_code=1
    check_cluster_version || exit_code=1
    check_cluster_resources || exit_code=1
    check_storage || exit_code=1
    check_ingress_controller || exit_code=1
    check_optional_components
    
    if [ $exit_code -eq 0 ]; then
        test_pvc_creation || exit_code=1
    fi
    
    # Summary
    print_header "Summary"
    
    if [ $exit_code -eq 0 ]; then
        print_success "Cluster is ready for AzurePhotoFlow deployment!"
        echo ""
        print_status $GREEN "✅ Next steps:"
        echo "1. Run: ./scripts/setup-secrets.sh"
        echo "2. Configure your domain in k8s/configmap.yaml and k8s/ingress.yaml"
        echo "3. Deploy: ./scripts/deploy-k8s.sh production latest"
    else
        print_error "Cluster needs additional configuration"
        echo ""
        show_installation_commands
    fi
    
    return $exit_code
}

# Check for required tools
if ! command_exists jq; then
    print_warning "jq not found. Installing for better JSON parsing..."
    if command_exists apt-get; then
        sudo apt-get update && sudo apt-get install -y jq
    elif command_exists yum; then
        sudo yum install -y jq
    elif command_exists brew; then
        brew install jq
    else
        print_warning "Please install jq manually for better output formatting"
    fi
fi

# Run main function
main "$@"