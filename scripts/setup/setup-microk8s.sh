#!/bin/bash

# MicroK8s setup script for AzurePhotoFlow
# This script enables required and recommended addons

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

# Check if MicroK8s is installed and running
check_microk8s() {
    if ! command -v microk8s >/dev/null 2>&1; then
        print_error "MicroK8s is not installed"
        echo "Install with: sudo snap install microk8s --classic"
        exit 1
    fi
    
    if ! microk8s status --wait-ready --timeout=30 >/dev/null 2>&1; then
        print_error "MicroK8s is not ready"
        echo "Start MicroK8s: microk8s start"
        exit 1
    fi
    
    print_success "MicroK8s is ready"
}

# Enable required addons
enable_required_addons() {
    print_status "Enabling required MicroK8s addons..."
    
    # DNS - Required for service discovery
    if ! microk8s status | grep -q "^dns: enabled"; then
        print_status "Enabling DNS addon..."
        microk8s enable dns
        print_success "DNS addon enabled"
    else
        print_success "DNS addon already enabled"
    fi
    
    # Storage - Required for persistent volumes
    if ! microk8s status | grep -q "^storage: enabled"; then
        print_status "Enabling storage addon..."
        microk8s enable storage
        print_success "Storage addon enabled"
    else
        print_success "Storage addon already enabled"
    fi
    
    # Wait for DNS to be ready
    print_status "Waiting for DNS to be ready..."
    microk8s kubectl wait --for=condition=ready pod -l k8s-app=kube-dns -n kube-system --timeout=120s
}

# Enable ingress addon
enable_ingress() {
    print_status "Enabling ingress addon..."
    
    if ! microk8s status | grep -q "^ingress: enabled"; then
        microk8s enable ingress
        print_success "Ingress addon enabled"
        
        # Wait for ingress controller to be ready
        print_status "Waiting for ingress controller to be ready..."
        microk8s kubectl wait --namespace ingress \
            --for=condition=ready pod \
            --selector=name=nginx-ingress-microk8s \
            --timeout=120s
        
        print_success "Ingress controller is ready"
    else
        print_success "Ingress addon already enabled"
    fi
}

# Enable cert-manager addon
enable_cert_manager() {
    print_status "Enabling cert-manager addon..."
    
    if ! microk8s status | grep -q "^cert-manager: enabled"; then
        microk8s enable cert-manager
        print_success "cert-manager addon enabled"
        
        # Wait for cert-manager to be ready
        print_status "Waiting for cert-manager to be ready..."
        microk8s kubectl wait --for=condition=available --timeout=300s deployment/cert-manager -n cert-manager
        microk8s kubectl wait --for=condition=available --timeout=300s deployment/cert-manager-cainjector -n cert-manager
        microk8s kubectl wait --for=condition=available --timeout=300s deployment/cert-manager-webhook -n cert-manager
        
        # Create ClusterIssuer for Let's Encrypt
        create_cluster_issuer
        
        print_success "cert-manager is ready"
    else
        print_success "cert-manager addon already enabled"
    fi
}

# Create ClusterIssuer for Let's Encrypt
create_cluster_issuer() {
    print_status "Creating Let's Encrypt ClusterIssuer..."
    
    # Check if ClusterIssuer already exists
    if microk8s kubectl get clusterissuer letsencrypt-prod >/dev/null 2>&1; then
        print_success "ClusterIssuer already exists"
        return
    fi
    
    # Prompt for email
    if [ -z "$LETSENCRYPT_EMAIL" ]; then
        read -p "Enter your email for Let's Encrypt notifications: " LETSENCRYPT_EMAIL
    fi
    
    cat <<EOF | microk8s kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: $LETSENCRYPT_EMAIL
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
    - http01:
        ingress:
          class: public
EOF
    
    print_success "ClusterIssuer created"
}

# Enable metrics-server addon
enable_metrics_server() {
    print_status "Enabling metrics-server addon..."
    
    if ! microk8s status | grep -q "^metrics-server: enabled"; then
        microk8s enable metrics-server
        print_success "metrics-server addon enabled"
        
        # Wait for metrics-server to be ready
        print_status "Waiting for metrics-server to be ready..."
        microk8s kubectl wait --for=condition=available --timeout=120s deployment/metrics-server -n kube-system
        
        print_success "metrics-server is ready"
    else
        print_success "metrics-server addon already enabled"
    fi
}

# Enable MetalLB for LoadBalancer services (optional)
enable_metallb() {
    print_status "Checking MetalLB configuration..."
    
    if ! microk8s status | grep -q "^metallb: enabled"; then
        print_warning "MetalLB not enabled (LoadBalancer services will be pending)"
        echo ""
        echo "To enable LoadBalancer support:"
        echo "1. Choose an IP range from your local network"
        echo "2. Run: microk8s enable metallb:START_IP-END_IP"
        echo "   Example: microk8s enable metallb:192.168.1.240-192.168.1.250"
        echo ""
        read -p "Do you want to enable MetalLB now? (y/N): " enable_lb
        
        if [[ $enable_lb =~ ^[Yy]$ ]]; then
            read -p "Enter IP range (e.g., 192.168.1.240-192.168.1.250): " ip_range
            if [ -n "$ip_range" ]; then
                microk8s enable metallb:$ip_range
                print_success "MetalLB enabled with IP range: $ip_range"
            else
                print_warning "No IP range provided, skipping MetalLB"
            fi
        else
            print_warning "MetalLB not enabled - you can enable it later if needed"
        fi
    else
        print_success "MetalLB addon already enabled"
    fi
}

# Setup kubectl access
setup_kubectl() {
    print_status "Setting up kubectl access..."
    
    # Check if ~/.kube directory exists
    if [ ! -d "$HOME/.kube" ]; then
        mkdir -p "$HOME/.kube"
    fi
    
    # Backup existing config if it exists
    if [ -f "$HOME/.kube/config" ]; then
        print_warning "Backing up existing kubectl config"
        cp "$HOME/.kube/config" "$HOME/.kube/config.backup.$(date +%s)"
    fi
    
    # Generate new config
    microk8s config > "$HOME/.kube/config"
    print_success "kubectl config updated"
    
    # Test kubectl access
    if kubectl get nodes >/dev/null 2>&1; then
        print_success "kubectl is working with MicroK8s"
    else
        print_warning "kubectl not working, you can use 'microk8s kubectl' instead"
        echo "Or add alias: echo \"alias kubectl='microk8s kubectl'\" >> ~/.bashrc"
    fi
}

# Show final status
show_status() {
    print_status "Final MicroK8s status:"
    echo ""
    microk8s status
    echo ""
    
    print_status "Cluster info:"
    microk8s kubectl cluster-info
    echo ""
    
    print_status "Available storage classes:"
    microk8s kubectl get storageclass
    echo ""
    
    print_status "Ingress controller status:"
    microk8s kubectl get pods -n ingress
    echo ""
    
    if microk8s status | grep -q "^cert-manager: enabled"; then
        print_status "cert-manager status:"
        microk8s kubectl get pods -n cert-manager
        echo ""
    fi
}

# Main setup function
main() {
    print_status "🚀 Setting up MicroK8s for AzurePhotoFlow"
    print_status "========================================"
    echo ""
    
    check_microk8s
    echo ""
    
    enable_required_addons
    echo ""
    
    enable_ingress
    echo ""
    
    enable_cert_manager
    echo ""
    
    enable_metrics_server
    echo ""
    
    enable_metallb
    echo ""
    
    setup_kubectl
    echo ""
    
    show_status
    
    print_success "MicroK8s setup completed!"
    echo ""
    print_status "Next steps:"
    echo "1. Run: ./scripts/prepare-microk8s.sh (to verify setup)"
    echo "2. Run: ./scripts/setup-secrets.sh"
    echo "3. Configure your domain in k8s/configmap.yaml and k8s/ingress.yaml"
    echo "4. Deploy: ./scripts/deploy-k8s.sh production latest"
}

# Handle command line arguments
case "${1:-setup}" in
    "setup")
        main
        ;;
    "enable-metallb")
        enable_metallb
        ;;
    "kubectl")
        setup_kubectl
        ;;
    "status")
        show_status
        ;;
    "help"|*)
        echo "Usage: $0 [command]"
        echo ""
        echo "Commands:"
        echo "  setup         - Full MicroK8s setup (default)"
        echo "  enable-metallb- Enable MetalLB for LoadBalancer services"
        echo "  kubectl       - Setup kubectl access"
        echo "  status        - Show current status"
        echo "  help          - Show this help"
        ;;
esac