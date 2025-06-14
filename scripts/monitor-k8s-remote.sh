#!/bin/bash

# Remote Kubernetes monitoring script for AzurePhotoFlow
# This script monitors a remote MicroK8s cluster via SSH

set -e

# SSH connection settings
SSH_USER=${SSH_USER:-""}
SSH_HOST=${SSH_HOST:-""}
SSH_PORT=${SSH_PORT:-22}
SSH_KEY=${SSH_KEY:-""}
SSH_OPTIONS="-o StrictHostKeyChecking=no -o UserKnownHostsFile=/dev/null -o LogLevel=ERROR -o ControlMaster=auto -o ControlPath=~/.ssh/control-%r@%h:%p -o ControlPersist=10m"
NAMESPACE="azurephotoflow"

# Color codes
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

print_header() { echo -e "${BOLD}${BLUE}=== $1 ===${NC}"; }
print_status() { echo -e "${CYAN}$1${NC}"; }
print_success() { echo -e "${GREEN}✅ $1${NC}"; }
print_error() { echo -e "${RED}❌ $1${NC}"; }
print_warning() { echo -e "${YELLOW}⚠️  $1${NC}"; }

# Function to show usage
show_usage() {
    echo "Usage: $0 [OPTIONS] [COMMAND]"
    echo ""
    echo "Options:"
    echo "  -h, --host HOST       Remote server hostname/IP"
    echo "  -u, --user USER       SSH username"
    echo "  -p, --port PORT       SSH port (default: 22)"
    echo "  -k, --key KEY_PATH    SSH private key path"
    echo "  --help                Show this help message"
    echo ""
    echo "Commands:"
    echo "  status               Show overall cluster status (default)"
    echo "  pods                 Show detailed pod information"
    echo "  services             Show services and endpoints"
    echo "  ingress              Show ingress configuration"
    echo "  logs [pod-name]      Show logs for a specific pod"
    echo "  events               Show recent cluster events"
    echo "  resources            Show resource usage"
    echo "  health               Run health checks"
    echo "  watch                Watch pod status in real-time"
    echo ""
    echo "Environment variables:"
    echo "  SSH_HOST              Remote server hostname/IP"
    echo "  SSH_USER              SSH username"
    echo "  SSH_PORT              SSH port (default: 22)"
    echo "  SSH_KEY               SSH private key path"
    echo ""
    echo "Examples:"
    echo "  $0 -h 192.168.1.100 -u ubuntu status"
    echo "  $0 logs backend-deployment-abc123"
    echo "  SSH_HOST=k8s-server.local SSH_USER=admin $0 health"
}

# Parse command line arguments
COMMAND="status"
while [[ $# -gt 0 ]]; do
    case $1 in
        -h|--host)
            SSH_HOST="$2"
            shift 2
            ;;
        -u|--user)
            SSH_USER="$2"
            shift 2
            ;;
        -p|--port)
            SSH_PORT="$2"
            shift 2
            ;;
        -k|--key)
            SSH_KEY="$2"
            shift 2
            ;;
        --help)
            show_usage
            exit 0
            ;;
        status|pods|services|ingress|logs|events|resources|health|watch)
            COMMAND="$1"
            shift
            if [[ "$COMMAND" == "logs" && $# -gt 0 ]]; then
                POD_NAME="$1"
                shift
            fi
            ;;
        *)
            print_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Validate SSH parameters
if [ -z "$SSH_HOST" ] || [ -z "$SSH_USER" ]; then
    print_error "SSH host and user are required"
    echo "Set SSH_HOST and SSH_USER environment variables or use -h and -u options"
    show_usage
    exit 1
fi

# Build SSH command
SSH_CMD="ssh"
if [ -n "$SSH_KEY" ]; then
    SSH_CMD="$SSH_CMD -i $SSH_KEY"
fi
SSH_CMD="$SSH_CMD $SSH_OPTIONS -p $SSH_PORT $SSH_USER@$SSH_HOST"

# Function to execute remote commands
remote_exec() {
    local command="$1"
    $SSH_CMD "$command"
}

# Function to check SSH connectivity
check_connection() {
    if ! remote_exec "echo 'Connected'" >/dev/null 2>&1; then
        print_error "Cannot establish SSH connection to $SSH_USER@$SSH_HOST"
        exit 1
    fi
}

# Function to show cluster status
show_status() {
    print_header "AzurePhotoFlow Cluster Status"
    echo "Remote Server: $SSH_USER@$SSH_HOST:$SSH_PORT"
    echo "Namespace: $NAMESPACE"
    echo ""
    
    # Check if namespace exists
    if ! remote_exec "microk8s kubectl get namespace $NAMESPACE >/dev/null 2>&1"; then
        print_error "Namespace '$NAMESPACE' not found"
        echo "Deploy the application first: ./scripts/deploy-k8s-remote.sh"
        return 1
    fi
    
    print_status "📊 Pod Status:"
    remote_exec "microk8s kubectl get pods -n $NAMESPACE -o wide"
    echo ""
    
    print_status "🔗 Services:"
    remote_exec "microk8s kubectl get svc -n $NAMESPACE"
    echo ""
    
    print_status "🌐 Ingress:"
    remote_exec "microk8s kubectl get ingress -n $NAMESPACE"
    echo ""
    
    # Show quick health summary
    local total_pods=$(remote_exec "microk8s kubectl get pods -n $NAMESPACE --no-headers | wc -l")
    local ready_pods=$(remote_exec "microk8s kubectl get pods -n $NAMESPACE --no-headers | grep -c Running || echo 0")
    
    echo "Health Summary:"
    if [ "$ready_pods" -eq "$total_pods" ] && [ "$total_pods" -gt 0 ]; then
        print_success "All $total_pods pods are running"
    else
        print_warning "$ready_pods/$total_pods pods are running"
    fi
}

# Function to show detailed pod information
show_pods() {
    print_header "Detailed Pod Information"
    
    print_status "📋 Pod List:"
    remote_exec "microk8s kubectl get pods -n $NAMESPACE -o wide"
    echo ""
    
    print_status "🔍 Pod Details:"
    local pods=$(remote_exec "microk8s kubectl get pods -n $NAMESPACE --no-headers -o custom-columns=':metadata.name'")
    
    for pod in $pods; do
        echo ""
        echo "Pod: $pod"
        echo "---"
        remote_exec "microk8s kubectl describe pod $pod -n $NAMESPACE | grep -A 10 -E '(Status|Ready|Restarts|Events:|Conditions:)'"
    done
}

# Function to show services and endpoints
show_services() {
    print_header "Services and Endpoints"
    
    print_status "🔗 Services:"
    remote_exec "microk8s kubectl get svc -n $NAMESPACE -o wide"
    echo ""
    
    print_status "🎯 Endpoints:"
    remote_exec "microk8s kubectl get endpoints -n $NAMESPACE"
    echo ""
    
    print_status "📡 Service Details:"
    local services=$(remote_exec "microk8s kubectl get svc -n $NAMESPACE --no-headers -o custom-columns=':metadata.name'")
    
    for svc in $services; do
        echo ""
        echo "Service: $svc"
        echo "---"
        remote_exec "microk8s kubectl describe svc $svc -n $NAMESPACE"
    done
}

# Function to show ingress information
show_ingress() {
    print_header "Ingress Configuration"
    
    print_status "🌐 Ingress Resources:"
    remote_exec "microk8s kubectl get ingress -n $NAMESPACE -o wide"
    echo ""
    
    if remote_exec "microk8s kubectl get ingress -n $NAMESPACE --no-headers | wc -l" | grep -q "^[1-9]"; then
        print_status "📋 Ingress Details:"
        remote_exec "microk8s kubectl describe ingress -n $NAMESPACE"
        echo ""
        
        print_status "🔒 TLS Certificates:"
        remote_exec "microk8s kubectl get certificates -n $NAMESPACE 2>/dev/null || echo 'No certificates found'"
    else
        print_warning "No ingress resources found"
    fi
    
    # Check ingress controller status
    print_status "🎛️ Ingress Controller Status:"
    remote_exec "microk8s kubectl get pods -n ingress 2>/dev/null || echo 'Ingress namespace not found'"
}

# Function to show logs
show_logs() {
    if [ -z "$POD_NAME" ]; then
        print_header "Available Pods for Logs"
        remote_exec "microk8s kubectl get pods -n $NAMESPACE"
        echo ""
        echo "Usage: $0 logs <pod-name>"
        return 1
    fi
    
    print_header "Logs for Pod: $POD_NAME"
    
    # Check if pod exists
    if ! remote_exec "microk8s kubectl get pod $POD_NAME -n $NAMESPACE >/dev/null 2>&1"; then
        print_error "Pod '$POD_NAME' not found in namespace '$NAMESPACE'"
        echo ""
        echo "Available pods:"
        remote_exec "microk8s kubectl get pods -n $NAMESPACE"
        return 1
    fi
    
    # Show recent logs
    print_status "📝 Recent logs (last 100 lines):"
    remote_exec "microk8s kubectl logs --tail=100 $POD_NAME -n $NAMESPACE"
    
    echo ""
    print_status "For live logs, run:"
    echo "ssh $SSH_USER@$SSH_HOST 'microk8s kubectl logs -f $POD_NAME -n $NAMESPACE'"
}

# Function to show recent events
show_events() {
    print_header "Recent Cluster Events"
    
    print_status "📅 Namespace Events:"
    remote_exec "microk8s kubectl get events -n $NAMESPACE --sort-by=.metadata.creationTimestamp"
    
    echo ""
    print_status "⚠️ Warning Events:"
    remote_exec "microk8s kubectl get events -n $NAMESPACE --field-selector type=Warning --sort-by=.metadata.creationTimestamp"
}

# Function to show resource usage
show_resources() {
    print_header "Resource Usage"
    
    # Check if metrics-server is available
    if remote_exec "microk8s kubectl top nodes >/dev/null 2>&1"; then
        print_status "🖥️ Node Resources:"
        remote_exec "microk8s kubectl top nodes"
        echo ""
        
        print_status "📊 Pod Resources:"
        remote_exec "microk8s kubectl top pods -n $NAMESPACE" || print_warning "Pod metrics not available"
    else
        print_warning "Metrics server not available"
        echo "Enable with: ssh $SSH_USER@$SSH_HOST 'microk8s enable metrics-server'"
    fi
    
    echo ""
    print_status "💾 Storage Usage:"
    remote_exec "microk8s kubectl get pv"
    echo ""
    remote_exec "microk8s kubectl get pvc -n $NAMESPACE"
}

# Function to run health checks
run_health_checks() {
    print_header "Health Checks"
    
    local health_score=0
    local total_checks=0
    
    # Check 1: All pods running
    total_checks=$((total_checks + 1))
    local total_pods=$(remote_exec "microk8s kubectl get pods -n $NAMESPACE --no-headers | wc -l")
    local ready_pods=$(remote_exec "microk8s kubectl get pods -n $NAMESPACE --no-headers | grep -c Running || echo 0")
    
    if [ "$ready_pods" -eq "$total_pods" ] && [ "$total_pods" -gt 0 ]; then
        print_success "All pods are running ($ready_pods/$total_pods)"
        health_score=$((health_score + 1))
    else
        print_error "Not all pods are running ($ready_pods/$total_pods)"
    fi
    
    # Check 2: Services have endpoints
    total_checks=$((total_checks + 1))
    local services_with_endpoints=$(remote_exec "microk8s kubectl get endpoints -n $NAMESPACE --no-headers | grep -v '<none>' | wc -l")
    local total_services=$(remote_exec "microk8s kubectl get svc -n $NAMESPACE --no-headers | wc -l")
    
    if [ "$services_with_endpoints" -eq "$total_services" ]; then
        print_success "All services have endpoints"
        health_score=$((health_score + 1))
    else
        print_warning "Some services may not have endpoints"
    fi
    
    # Check 3: Ingress has external IP
    total_checks=$((total_checks + 1))
    if remote_exec "microk8s kubectl get ingress -n $NAMESPACE -o jsonpath='{.items[*].status.loadBalancer.ingress[*].ip}' | grep -q ."; then
        print_success "Ingress has external IP"
        health_score=$((health_score + 1))
    else
        print_warning "Ingress does not have external IP (may use NodePort)"
    fi
    
    # Check 4: No failing pods
    total_checks=$((total_checks + 1))
    local failed_pods=$(remote_exec "microk8s kubectl get pods -n $NAMESPACE --no-headers | grep -E '(Error|CrashLoopBackOff|ImagePullBackOff)' | wc -l")
    
    if [ "$failed_pods" -eq 0 ]; then
        print_success "No failed pods"
        health_score=$((health_score + 1))
    else
        print_error "$failed_pods pods are in failed state"
    fi
    
    # Health summary
    echo ""
    local health_percentage=$((health_score * 100 / total_checks))
    print_status "🎯 Overall Health: $health_score/$total_checks checks passed ($health_percentage%)"
    
    if [ "$health_score" -eq "$total_checks" ]; then
        print_success "Cluster is healthy!"
    elif [ "$health_score" -ge $((total_checks * 3 / 4)) ]; then
        print_warning "Cluster has minor issues"
    else
        print_error "Cluster has significant issues"
    fi
}

# Function to watch pod status
watch_pods() {
    print_header "Watching Pod Status (Press Ctrl+C to exit)"
    echo "Remote Server: $SSH_USER@$SSH_HOST"
    echo "Namespace: $NAMESPACE"
    echo ""
    
    # Use remote watch command
    remote_exec "watch -n 2 'microk8s kubectl get pods -n $NAMESPACE'"
}

# Main execution
main() {
    check_connection
    
    case $COMMAND in
        "status")
            show_status
            ;;
        "pods")
            show_pods
            ;;
        "services")
            show_services
            ;;
        "ingress")
            show_ingress
            ;;
        "logs")
            show_logs
            ;;
        "events")
            show_events
            ;;
        "resources")
            show_resources
            ;;
        "health")
            run_health_checks
            ;;
        "watch")
            watch_pods
            ;;
        *)
            print_error "Unknown command: $COMMAND"
            show_usage
            exit 1
            ;;
    esac
}

# Run main function
main "$@"