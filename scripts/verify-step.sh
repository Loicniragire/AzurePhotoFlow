#!/bin/bash

# Quick verification script for individual preparation steps
# Usage: ./scripts/verify-step.sh [step-name]

set -e

# Color codes
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m'

print_success() { echo -e "${GREEN}✅ $1${NC}"; }
print_error() { echo -e "${RED}❌ $1${NC}"; }
print_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }

STEP=${1:-"all"}

verify_kubectl() {
    echo "Verifying kubectl connectivity..."
    if kubectl cluster-info >/dev/null 2>&1; then
        print_success "kubectl connectivity works"
        return 0
    else
        print_error "kubectl cannot connect to cluster"
        return 1
    fi
}

verify_nodes() {
    echo "Verifying cluster nodes..."
    local ready_nodes=$(kubectl get nodes --no-headers | grep -c " Ready" || echo "0")
    if [ "$ready_nodes" -gt 0 ]; then
        print_success "$ready_nodes node(s) ready"
        kubectl get nodes
        return 0
    else
        print_error "No ready nodes found"
        return 1
    fi
}

verify_ingress() {
    echo "Verifying NGINX Ingress Controller..."
    if kubectl get pods -n ingress-nginx -l app.kubernetes.io/name=ingress-nginx --no-headers | grep -q "Running"; then
        print_success "NGINX Ingress Controller is running"
        kubectl get pods -n ingress-nginx -l app.kubernetes.io/name=ingress-nginx
        
        # Check for external IP
        local external_ip=$(kubectl get svc ingress-nginx-controller -n ingress-nginx -o jsonpath='{.status.loadBalancer.ingress[0].ip}' 2>/dev/null || echo "")
        if [ -n "$external_ip" ]; then
            print_success "External IP assigned: $external_ip"
        else
            print_warning "No external IP assigned yet (may take a few minutes)"
        fi
        return 0
    else
        print_error "NGINX Ingress Controller not running"
        echo "Install with: kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.8.2/deploy/static/provider/cloud/deploy.yaml"
        return 1
    fi
}

verify_storage() {
    echo "Verifying storage configuration..."
    local storage_classes=$(kubectl get storageclass --no-headers 2>/dev/null | wc -l)
    if [ "$storage_classes" -gt 0 ]; then
        print_success "Storage class(es) available"
        kubectl get storageclass
        return 0
    else
        print_error "No storage classes found"
        return 1
    fi
}

verify_certmanager() {
    echo "Verifying cert-manager (optional)..."
    if kubectl get namespace cert-manager >/dev/null 2>&1; then
        local running_pods=$(kubectl get pods -n cert-manager --no-headers | grep -c "Running" || echo "0")
        if [ "$running_pods" -ge 3 ]; then
            print_success "cert-manager is running ($running_pods/3 pods)"
            return 0
        else
            print_warning "cert-manager pods not all running ($running_pods/3)"
            return 1
        fi
    else
        print_warning "cert-manager not installed (optional)"
        return 0
    fi
}

verify_metrics() {
    echo "Verifying metrics-server (optional)..."
    if kubectl top nodes >/dev/null 2>&1; then
        print_success "metrics-server is working"
        return 0
    else
        print_warning "metrics-server not available (optional)"
        return 0
    fi
}

test_pvc() {
    echo "Testing PVC creation..."
    local test_pvc="test-pvc-verify-$$"
    
    cat <<EOF | kubectl apply -f - >/dev/null
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: $test_pvc
  namespace: default
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 1Gi
EOF

    sleep 5
    
    if kubectl get pvc $test_pvc -o jsonpath='{.status.phase}' | grep -q "Bound"; then
        print_success "PVC creation works"
        kubectl delete pvc $test_pvc >/dev/null 2>&1
        return 0
    else
        print_error "PVC failed to bind"
        kubectl describe pvc $test_pvc
        kubectl delete pvc $test_pvc >/dev/null 2>&1
        return 1
    fi
}

case $STEP in
    "kubectl")
        verify_kubectl
        ;;
    "nodes")
        verify_nodes
        ;;
    "ingress")
        verify_ingress
        ;;
    "storage")
        verify_storage
        ;;
    "certmanager")
        verify_certmanager
        ;;
    "metrics")
        verify_metrics
        ;;
    "pvc")
        test_pvc
        ;;
    "all")
        echo "🔍 Running all verification checks..."
        echo ""
        
        exit_code=0
        verify_kubectl || exit_code=1
        echo ""
        verify_nodes || exit_code=1
        echo ""
        verify_storage || exit_code=1
        echo ""
        verify_ingress || exit_code=1
        echo ""
        verify_certmanager
        echo ""
        verify_metrics
        echo ""
        test_pvc || exit_code=1
        
        echo ""
        if [ $exit_code -eq 0 ]; then
            print_success "All critical checks passed! Cluster is ready."
        else
            print_error "Some critical checks failed. See details above."
        fi
        
        exit $exit_code
        ;;
    *)
        echo "Usage: $0 [kubectl|nodes|ingress|storage|certmanager|metrics|pvc|all]"
        echo ""
        echo "Available verification steps:"
        echo "  kubectl     - Verify kubectl connectivity"
        echo "  nodes       - Verify cluster nodes are ready"
        echo "  ingress     - Verify NGINX Ingress Controller"
        echo "  storage     - Verify storage classes"
        echo "  certmanager - Verify cert-manager (optional)"
        echo "  metrics     - Verify metrics-server (optional)"
        echo "  pvc         - Test PVC creation and binding"
        echo "  all         - Run all checks (default)"
        exit 1
        ;;
esac