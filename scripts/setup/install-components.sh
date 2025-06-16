#!/bin/bash

# Install required and optional Kubernetes components for AzurePhotoFlow
# Usage: ./scripts/install-components.sh [component-name]

set -e

# Color codes
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

print_status() { echo -e "${BLUE}$1${NC}"; }
print_success() { echo -e "${GREEN}✅ $1${NC}"; }
print_error() { echo -e "${RED}❌ $1${NC}"; }
print_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }

COMPONENT=${1:-"help"}

install_nginx_ingress() {
    print_status "Installing NGINX Ingress Controller..."
    
    # Check if already installed
    if kubectl get namespace ingress-nginx >/dev/null 2>&1; then
        print_warning "NGINX Ingress Controller namespace already exists"
        return 0
    fi
    
    # Install NGINX Ingress Controller
    kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.8.2/deploy/static/provider/cloud/deploy.yaml
    
    print_status "Waiting for NGINX Ingress Controller to be ready..."
    kubectl wait --namespace ingress-nginx \
        --for=condition=ready pod \
        --selector=app.kubernetes.io/component=controller \
        --timeout=300s
    
    if [ $? -eq 0 ]; then
        print_success "NGINX Ingress Controller installed successfully"
        kubectl get pods -n ingress-nginx
        kubectl get svc -n ingress-nginx
    else
        print_error "NGINX Ingress Controller installation failed"
        return 1
    fi
}

install_cert_manager() {
    print_status "Installing cert-manager..."
    
    # Check if already installed
    if kubectl get namespace cert-manager >/dev/null 2>&1; then
        print_warning "cert-manager namespace already exists"
        return 0
    fi
    
    # Install cert-manager
    kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.2/cert-manager.yaml
    
    print_status "Waiting for cert-manager to be ready..."
    kubectl wait --for=condition=available --timeout=300s deployment/cert-manager -n cert-manager
    kubectl wait --for=condition=available --timeout=300s deployment/cert-manager-cainjector -n cert-manager
    kubectl wait --for=condition=available --timeout=300s deployment/cert-manager-webhook -n cert-manager
    
    if [ $? -eq 0 ]; then
        print_success "cert-manager installed successfully"
        kubectl get pods -n cert-manager
        
        # Create ClusterIssuer
        print_status "Creating Let's Encrypt ClusterIssuer..."
        read -p "Enter your email for Let's Encrypt notifications: " EMAIL
        
        cat <<EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: $EMAIL
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
    - http01:
        ingress:
          class: nginx
EOF
        
        print_success "ClusterIssuer created"
    else
        print_error "cert-manager installation failed"
        return 1
    fi
}

install_metrics_server() {
    print_status "Installing metrics-server..."
    
    # Check if already installed
    if kubectl get deployment metrics-server -n kube-system >/dev/null 2>&1; then
        print_warning "metrics-server already exists"
        return 0
    fi
    
    # Install metrics-server
    kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml
    
    print_status "Waiting for metrics-server to be ready..."
    kubectl wait --for=condition=available --timeout=300s deployment/metrics-server -n kube-system
    
    if [ $? -eq 0 ]; then
        print_success "metrics-server installed successfully"
        sleep 10  # Wait for metrics to be collected
        kubectl top nodes
    else
        print_error "metrics-server installation failed"
        return 1
    fi
}

create_storage_class() {
    print_status "Creating default storage class..."
    
    # Check if default storage class exists
    local default_sc=$(kubectl get storageclass -o jsonpath='{.items[?(@.metadata.annotations.storageclass\.kubernetes\.io/is-default-class=="true")].metadata.name}' 2>/dev/null)
    if [ -n "$default_sc" ]; then
        print_warning "Default storage class already exists: $default_sc"
        return 0
    fi
    
    echo "Available storage provisioners:"
    echo "1. local-path (for single-node clusters)"
    echo "2. hostpath (for development)"
    echo "3. nfs (if you have NFS server)"
    echo "4. Skip (configure manually)"
    
    read -p "Choose storage type (1-4): " choice
    
    case $choice in
        1)
            # Install local-path-provisioner
            kubectl apply -f https://raw.githubusercontent.com/rancher/local-path-provisioner/v0.0.24/deploy/local-path-storage.yaml
            kubectl patch storageclass local-path -p '{"metadata": {"annotations":{"storageclass.kubernetes.io/is-default-class":"true"}}}'
            print_success "local-path storage class created and set as default"
            ;;
        2)
            cat <<EOF | kubectl apply -f -
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: hostpath
  annotations:
    storageclass.kubernetes.io/is-default-class: "true"
provisioner: kubernetes.io/host-path
parameters:
  type: DirectoryOrCreate
EOF
            print_success "hostpath storage class created and set as default"
            ;;
        3)
            read -p "Enter NFS server IP: " NFS_SERVER
            read -p "Enter NFS path: " NFS_PATH
            cat <<EOF | kubectl apply -f -
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: nfs
  annotations:
    storageclass.kubernetes.io/is-default-class: "true"
provisioner: kubernetes.io/nfs
parameters:
  server: $NFS_SERVER
  path: $NFS_PATH
EOF
            print_success "NFS storage class created and set as default"
            ;;
        4)
            print_warning "Skipping storage class creation"
            ;;
        *)
            print_error "Invalid choice"
            return 1
            ;;
    esac
}

install_all() {
    print_status "Installing all required and recommended components..."
    
    exit_code=0
    
    install_nginx_ingress || exit_code=1
    echo ""
    
    install_cert_manager || exit_code=1
    echo ""
    
    install_metrics_server || exit_code=1
    echo ""
    
    create_storage_class || exit_code=1
    echo ""
    
    if [ $exit_code -eq 0 ]; then
        print_success "All components installed successfully!"
        echo ""
        print_status "Running verification..."
        ./scripts/verify-step.sh all
    else
        print_error "Some components failed to install"
    fi
    
    return $exit_code
}

show_help() {
    echo "Usage: $0 [component]"
    echo ""
    echo "Available components:"
    echo "  nginx-ingress   - Install NGINX Ingress Controller (required)"
    echo "  cert-manager    - Install cert-manager for SSL certificates (recommended)"
    echo "  metrics-server  - Install metrics-server for monitoring (recommended)"
    echo "  storage-class   - Create default storage class (required)"
    echo "  all            - Install all components (recommended)"
    echo "  help           - Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0 nginx-ingress    # Install only NGINX Ingress"
    echo "  $0 all             # Install everything"
}

case $COMPONENT in
    "nginx-ingress")
        install_nginx_ingress
        ;;
    "cert-manager")
        install_cert_manager
        ;;
    "metrics-server")
        install_metrics_server
        ;;
    "storage-class")
        create_storage_class
        ;;
    "all")
        install_all
        ;;
    "help"|*)
        show_help
        ;;
esac