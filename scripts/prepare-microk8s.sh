#!/bin/bash

# MicroK8s-specific cluster preparation script for AzurePhotoFlow
# This script validates and prepares your MicroK8s cluster for deployment

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

# Function to check MicroK8s status
check_microk8s() {
    print_header "Checking MicroK8s status"
    
    if ! command_exists microk8s; then
        print_error "MicroK8s is not installed"
        echo "Install MicroK8s: sudo snap install microk8s --classic"
        return 1
    fi
    
    print_success "MicroK8s is installed"
    
    # Check MicroK8s status
    if microk8s status --wait-ready --timeout=30 >/dev/null 2>&1; then
        print_success "MicroK8s is running and ready"
    else
        print_error "MicroK8s is not ready"
        echo "Start MicroK8s: microk8s start"
        return 1
    fi
    
    # Show cluster info
    echo "MicroK8s cluster info:"
    microk8s kubectl cluster-info
}

# Function to check kubectl access
check_kubectl_access() {
    print_header "Checking kubectl access"
    
    # Check if microk8s kubectl works
    if microk8s kubectl get nodes >/dev/null 2>&1; then
        print_success "microk8s kubectl works"
    else
        print_error "Cannot access cluster with microk8s kubectl"
        return 1
    fi
    
    # Check if regular kubectl works (if alias is set up)
    if command_exists kubectl && kubectl get nodes >/dev/null 2>&1; then
        print_success "kubectl alias/config works"
        echo "Using: $(which kubectl)"
    else
        print_warning "kubectl not configured for MicroK8s"
        echo "Set up kubectl access:"
        echo "  microk8s config > ~/.kube/config"
        echo "  OR alias kubectl='microk8s kubectl'"
    fi
}

# Function to check MicroK8s addons
check_microk8s_addons() {
    print_header "Checking MicroK8s addons"
    
    echo "Current addon status:"
    microk8s status
    echo ""
    
    # Check required addons
    local required_addons=("dns" "storage")
    local optional_addons=("ingress" "cert-manager" "metrics-server" "registry")
    
    print_status $BLUE "Required addons:"
    for addon in "${required_addons[@]}"; do
        if microk8s status | grep -q "^$addon: enabled"; then
            print_success "$addon is enabled"
        else
            print_error "$addon is not enabled"
            echo "Enable with: microk8s enable $addon"
        fi
    done
    
    echo ""
    print_status $BLUE "Optional but recommended addons:"
    for addon in "${optional_addons[@]}"; do
        if microk8s status | grep -q "^$addon: enabled"; then
            print_success "$addon is enabled"
        else
            print_warning "$addon is not enabled (optional)"
            echo "Enable with: microk8s enable $addon"
        fi
    done
}

# Function to check cluster resources
check_cluster_resources() {
    print_header "Checking cluster resources"
    
    echo "Nodes:"
    microk8s kubectl get nodes -o wide
    echo ""
    
    # Check if metrics-server is available for resource monitoring
    if microk8s kubectl top nodes >/dev/null 2>&1; then
        echo "Resource usage:"
        microk8s kubectl top nodes
        print_success "Resource metrics available"
    else
        print_warning "Resource metrics not available"
        echo "Enable metrics-server: microk8s enable metrics-server"
    fi
}

# Function to check storage
check_storage() {
    print_header "Checking storage configuration"
    
    # Check if hostpath-storage addon is enabled
    if microk8s status | grep -q "^storage: enabled"; then
        print_success "MicroK8s storage addon is enabled"
        
        # Show storage classes
        echo "Available storage classes:"
        microk8s kubectl get storageclass
        
        # Check for default storage class
        local default_sc=$(microk8s kubectl get storageclass -o jsonpath='{.items[?(@.metadata.annotations.storageclass\.kubernetes\.io/is-default-class=="true")].metadata.name}' 2>/dev/null)
        if [ -n "$default_sc" ]; then
            print_success "Default storage class: $default_sc"
        else
            print_warning "No default storage class (MicroK8s usually sets one)"
        fi
    else
        print_error "MicroK8s storage addon not enabled"
        echo "Enable with: microk8s enable storage"
        return 1
    fi
}

# Function to check ingress
check_ingress() {
    print_header "Checking Ingress configuration"
    
    if microk8s status | grep -q "^ingress: enabled"; then
        print_success "MicroK8s ingress addon is enabled"
        
        # Check ingress controller pods
        local ingress_pods=$(microk8s kubectl get pods -n ingress --no-headers 2>/dev/null | wc -l)
        if [ "$ingress_pods" -gt 0 ]; then
            print_success "Ingress controller pods are running"
            microk8s kubectl get pods -n ingress
            
            # Check for ingress service
            echo ""
            echo "Ingress services:"
            microk8s kubectl get svc -n ingress
        else
            print_warning "Ingress pods not found in 'ingress' namespace"
            # Check other common namespaces
            microk8s kubectl get pods -A | grep -i ingress || true
        fi
    else
        print_error "MicroK8s ingress addon not enabled"
        echo "Enable with: microk8s enable ingress"
        return 1
    fi
}

# Function to check DNS
check_dns() {
    print_header "Checking DNS configuration"
    
    if microk8s status | grep -q "^dns: enabled"; then
        print_success "MicroK8s DNS addon is enabled"
        
        # Check CoreDNS pods
        local dns_pods=$(microk8s kubectl get pods -n kube-system -l k8s-app=kube-dns --no-headers | grep -c "Running" || echo "0")
        if [ "$dns_pods" -gt 0 ]; then
            print_success "DNS pods are running ($dns_pods)"
        else
            print_warning "DNS pods not running"
            microk8s kubectl get pods -n kube-system -l k8s-app=kube-dns
        fi
    else
        print_error "MicroK8s DNS addon not enabled"
        echo "Enable with: microk8s enable dns"
        return 1
    fi
}

# Function to test PVC creation
test_pvc_creation() {
    print_header "Testing PVC creation"
    
    local test_pvc_name="test-pvc-microk8s"
    
    # Create test PVC
    cat <<EOF | microk8s kubectl apply -f -
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
    if microk8s kubectl wait --for=condition=bound pvc/$test_pvc_name --timeout=60s >/dev/null 2>&1; then
        print_success "PVC creation and binding successful"
        
        # Get storage class used
        local storage_class=$(microk8s kubectl get pvc $test_pvc_name -o jsonpath='{.spec.storageClassName}')
        print_success "Storage class used: ${storage_class:-"(default)"}"
        
        # Show the PV that was created
        local pv_name=$(microk8s kubectl get pvc $test_pvc_name -o jsonpath='{.spec.volumeName}')
        if [ -n "$pv_name" ]; then
            echo "Created PersistentVolume: $pv_name"
            microk8s kubectl get pv $pv_name
        fi
    else
        print_error "PVC failed to bind within 60 seconds"
        microk8s kubectl describe pvc $test_pvc_name
    fi
    
    # Clean up
    microk8s kubectl delete pvc $test_pvc_name >/dev/null 2>&1 || true
}

# Function to show MicroK8s-specific commands
show_microk8s_commands() {
    print_header "MicroK8s-specific setup commands"
    
    echo "Enable required addons:"
    echo "  microk8s enable dns storage"
    echo ""
    
    echo "Enable recommended addons:"
    echo "  microk8s enable ingress cert-manager metrics-server"
    echo ""
    
    echo "Optional addons for development:"
    echo "  microk8s enable registry dashboard"
    echo ""
    
    echo "Configure kubectl access:"
    echo "  microk8s config > ~/.kube/config"
    echo "  OR add alias: alias kubectl='microk8s kubectl'"
    echo ""
    
    echo "Check addon status:"
    echo "  microk8s status"
    echo ""
    
    echo "Access services:"
    echo "  # For LoadBalancer services, MicroK8s uses MetalLB"
    echo "  microk8s enable metallb:10.64.140.43-10.64.140.49  # Adjust IP range"
}

# Main execution
main() {
    print_status $BLUE "🚀 MicroK8s Cluster Preparation for AzurePhotoFlow"
    print_status $BLUE "================================================"
    
    local exit_code=0
    
    # Run all checks
    check_microk8s || exit_code=1
    check_kubectl_access
    check_microk8s_addons || exit_code=1
    check_dns || exit_code=1
    check_storage || exit_code=1
    check_ingress || exit_code=1
    check_cluster_resources
    
    if [ $exit_code -eq 0 ]; then
        test_pvc_creation || exit_code=1
    fi
    
    # Summary
    print_header "Summary"
    
    if [ $exit_code -eq 0 ]; then
        print_success "MicroK8s cluster is ready for AzurePhotoFlow deployment!"
        echo ""
        print_status $GREEN "✅ Next steps:"
        echo "1. Configure kubectl: microk8s config > ~/.kube/config"
        echo "2. Run: ./scripts/setup-secrets.sh"
        echo "3. Configure your domain in k8s/configmap.yaml and k8s/ingress.yaml"
        echo "4. Deploy: ./scripts/deploy-k8s.sh production latest"
    else
        print_error "MicroK8s cluster needs additional configuration"
        echo ""
        show_microk8s_commands
    fi
    
    return $exit_code
}

# Run main function
main "$@"