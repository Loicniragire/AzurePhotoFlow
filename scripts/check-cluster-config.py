#!/usr/bin/env python3
"""
Cluster Configuration Checker for AzurePhotoFlow Pipeline
This script checks existing MicroK8s cluster state and generates deployment decisions.
"""

import json
import subprocess
import sys
import time
from typing import Dict, List, Optional, Tuple

class ClusterConfigChecker:
    def __init__(self, ssh_host: str, ssh_user: str, ssh_key: str = None, ssh_port: int = 22):
        self.ssh_host = ssh_host
        self.ssh_user = ssh_user
        self.ssh_key = ssh_key
        self.ssh_port = ssh_port
        self.ssh_base_cmd = self._build_ssh_cmd()
        self.config = {
            "cluster_ready": False,
            "namespaces": {},
            "secrets": {},
            "deployments": {},
            "services": {},
            "ingress": {},
            "storage_classes": {},
            "addons": {},
            "recommendations": [],
            "actions_needed": []
        }
    
    def _build_ssh_cmd(self) -> List[str]:
        """Build base SSH command with options."""
        cmd = ["ssh", "-o", "ConnectTimeout=10", "-o", "StrictHostKeyChecking=no", 
               "-o", "UserKnownHostsFile=/dev/null", "-o", "LogLevel=ERROR"]
        
        if self.ssh_key:
            cmd.extend(["-i", self.ssh_key])
        
        cmd.extend(["-p", str(self.ssh_port), f"{self.ssh_user}@{self.ssh_host}"])
        return cmd
    
    def _run_remote_cmd(self, command: str, timeout: int = 30) -> Tuple[bool, str, str]:
        """Execute command on remote server with timeout."""
        try:
            full_cmd = self.ssh_base_cmd + [command]
            result = subprocess.run(
                full_cmd, 
                capture_output=True, 
                text=True, 
                timeout=timeout
            )
            return result.returncode == 0, result.stdout.strip(), result.stderr.strip()
        except subprocess.TimeoutExpired:
            return False, "", f"Command timed out after {timeout}s"
        except Exception as e:
            return False, "", str(e)
    
    def check_basic_connectivity(self) -> bool:
        """Test basic SSH connectivity."""
        print("🔍 Checking SSH connectivity...")
        success, stdout, stderr = self._run_remote_cmd("echo 'SSH_OK'", timeout=10)
        if success and "SSH_OK" in stdout:
            print("✅ SSH connection established")
            return True
        else:
            print(f"❌ SSH connection failed: {stderr}")
            return False
    
    def check_microk8s_status(self) -> Dict:
        """Check MicroK8s installation and basic status."""
        print("🔍 Checking MicroK8s status...")
        status_info = {
            "installed": False,
            "running": False,
            "api_responsive": False,
            "version": None
        }
        
        # Check if MicroK8s is installed
        success, stdout, _ = self._run_remote_cmd("command -v microk8s", timeout=5)
        if not success:
            print("❌ MicroK8s not installed")
            self.config["actions_needed"].append("install_microk8s")
            return status_info
        
        status_info["installed"] = True
        print("✅ MicroK8s is installed")
        
        # Get version
        success, stdout, _ = self._run_remote_cmd("microk8s version --short", timeout=10)
        if success and stdout:
            status_info["version"] = stdout
            print(f"📊 MicroK8s version: {stdout}")
        
        # Check if running (quick check)
        success, stdout, _ = self._run_remote_cmd("pgrep -f 'kube-apiserver' > /dev/null && echo 'RUNNING'", timeout=5)
        if success and "RUNNING" in stdout:
            status_info["running"] = True
            print("✅ MicroK8s processes are running")
        else:
            print("⚠️  MicroK8s processes not detected")
            self.config["actions_needed"].append("start_microk8s")
        
        # Test API server responsiveness (quick test)
        success, stdout, _ = self._run_remote_cmd("microk8s kubectl version --client --output=json", timeout=10)
        if success:
            status_info["api_responsive"] = True
            print("✅ kubectl client is responsive")
        else:
            print("⚠️  kubectl client issues detected")
            self.config["actions_needed"].append("restart_microk8s")
        
        return status_info
    
    def check_addons(self) -> Dict:
        """Check enabled MicroK8s addons."""
        print("🔍 Checking MicroK8s addons...")
        addons_info = {}
        required_addons = ["dns", "storage", "ingress"]
        optional_addons = ["cert-manager", "metrics-server", "registry"]
        
        # Get addon status
        success, stdout, _ = self._run_remote_cmd("microk8s status --format yaml", timeout=15)
        if not success:
            # Fallback to simple status
            success, stdout, _ = self._run_remote_cmd("microk8s status", timeout=10)
        
        if success and stdout:
            lines = stdout.split('\n')
            for line in lines:
                if ': enabled' in line or ': disabled' in line:
                    addon_name = line.split(':')[0].strip()
                    status = 'enabled' if 'enabled' in line else 'disabled'
                    addons_info[addon_name] = status
        
        # Check required addons
        missing_required = []
        for addon in required_addons:
            # Handle addon name variations
            addon_variations = [addon]
            if addon == "storage":
                addon_variations.append("hostpath-storage")
            
            enabled = False
            for variation in addon_variations:
                if addons_info.get(variation) == "enabled":
                    enabled = True
                    break
            
            if enabled:
                print(f"✅ Required addon '{addon}' is enabled")
            else:
                print(f"❌ Required addon '{addon}' is missing")
                missing_required.append(addon)
        
        if missing_required:
            self.config["actions_needed"].append(f"enable_addons:{','.join(missing_required)}")
        
        # Check optional addons
        for addon in optional_addons:
            if addons_info.get(addon) == "enabled":
                print(f"✅ Optional addon '{addon}' is enabled")
            else:
                print(f"ℹ️  Optional addon '{addon}' is not enabled")
        
        return addons_info
    
    def check_namespace(self, namespace: str = "azurephotoflow") -> Dict:
        """Check if namespace exists and its contents."""
        print(f"🔍 Checking namespace '{namespace}'...")
        ns_info = {
            "exists": False,
            "secrets": [],
            "deployments": [],
            "services": [],
            "ingress": [],
            "pvcs": []
        }
        
        # Check if namespace exists
        success, stdout, _ = self._run_remote_cmd(f"microk8s kubectl get namespace {namespace} -o name", timeout=10)
        if success and namespace in stdout:
            ns_info["exists"] = True
            print(f"✅ Namespace '{namespace}' exists")
            
            # Get secrets
            success, stdout, _ = self._run_remote_cmd(f"microk8s kubectl get secrets -n {namespace} -o name", timeout=10)
            if success:
                ns_info["secrets"] = [s.replace("secret/", "") for s in stdout.split('\n') if s and not s.startswith("default-token")]
            
            # Get deployments
            success, stdout, _ = self._run_remote_cmd(f"microk8s kubectl get deployments -n {namespace} -o name", timeout=10)
            if success:
                ns_info["deployments"] = [d.replace("deployment/", "") for d in stdout.split('\n') if d]
            
            # Get services
            success, stdout, _ = self._run_remote_cmd(f"microk8s kubectl get services -n {namespace} -o name", timeout=10)
            if success:
                ns_info["services"] = [s.replace("service/", "") for s in stdout.split('\n') if s]
            
            # Get ingress
            success, stdout, _ = self._run_remote_cmd(f"microk8s kubectl get ingress -n {namespace} -o name", timeout=10)
            if success:
                ns_info["ingress"] = [i.replace("ingress/", "") for i in stdout.split('\n') if i]
            
            # Get PVCs
            success, stdout, _ = self._run_remote_cmd(f"microk8s kubectl get pvc -n {namespace} -o name", timeout=10)
            if success:
                ns_info["pvcs"] = [p.replace("persistentvolumeclaim/", "") for p in stdout.split('\n') if p]
            
            print(f"📊 Found: {len(ns_info['secrets'])} secrets, {len(ns_info['deployments'])} deployments, "
                  f"{len(ns_info['services'])} services, {len(ns_info['ingress'])} ingress")
        else:
            print(f"ℹ️  Namespace '{namespace}' does not exist")
            self.config["actions_needed"].append(f"create_namespace:{namespace}")
        
        return ns_info
    
    def check_secrets(self, namespace: str = "azurephotoflow") -> Dict:
        """Check for required application secrets."""
        print("🔍 Checking application secrets...")
        secrets_info = {
            "azurephotoflow-secrets": False,
            "registry-secret": False
        }
        
        if not self.config["namespaces"].get(namespace, {}).get("exists", False):
            print("ℹ️  Skipping secret check - namespace doesn't exist")
            return secrets_info
        
        for secret_name in secrets_info.keys():
            success, stdout, _ = self._run_remote_cmd(
                f"microk8s kubectl get secret {secret_name} -n {namespace} -o name", timeout=10
            )
            if success and secret_name in stdout:
                secrets_info[secret_name] = True
                print(f"✅ Secret '{secret_name}' exists")
            else:
                print(f"❌ Secret '{secret_name}' missing")
                self.config["actions_needed"].append(f"create_secret:{secret_name}")
        
        return secrets_info
    
    def check_deployments(self, namespace: str = "azurephotoflow") -> Dict:
        """Check deployment status."""
        print("🔍 Checking deployments...")
        deployment_info = {}
        expected_deployments = ["backend-deployment", "frontend-deployment", "minio-deployment", "qdrant-deployment"]
        
        if not self.config["namespaces"].get(namespace, {}).get("exists", False):
            print("ℹ️  Skipping deployment check - namespace doesn't exist")
            return deployment_info
        
        for deployment in expected_deployments:
            success, stdout, _ = self._run_remote_cmd(
                f"microk8s kubectl get deployment {deployment} -n {namespace} -o jsonpath='{{.status.readyReplicas}}/{{.spec.replicas}}'", 
                timeout=10
            )
            if success and stdout:
                deployment_info[deployment] = {
                    "exists": True,
                    "status": stdout,
                    "ready": "/" not in stdout or stdout.split("/")[0] == stdout.split("/")[1]
                }
                if deployment_info[deployment]["ready"]:
                    print(f"✅ Deployment '{deployment}' is ready ({stdout})")
                else:
                    print(f"⚠️  Deployment '{deployment}' not ready ({stdout})")
            else:
                deployment_info[deployment] = {"exists": False, "status": "missing", "ready": False}
                print(f"❌ Deployment '{deployment}' not found")
        
        return deployment_info
    
    def check_storage(self) -> Dict:
        """Check storage configuration."""
        print("🔍 Checking storage configuration...")
        storage_info = {
            "default_storage_class": None,
            "available_classes": [],
            "pv_count": 0
        }
        
        # Get storage classes
        success, stdout, _ = self._run_remote_cmd("microk8s kubectl get storageclass -o name", timeout=10)
        if success:
            storage_info["available_classes"] = [sc.replace("storageclass.storage.k8s.io/", "") for sc in stdout.split('\n') if sc]
        
        # Get default storage class
        success, stdout, _ = self._run_remote_cmd(
            "microk8s kubectl get storageclass -o jsonpath='{.items[?(@.metadata.annotations.storageclass\\.kubernetes\\.io/is-default-class==\"true\")].metadata.name}'", 
            timeout=10
        )
        if success and stdout:
            storage_info["default_storage_class"] = stdout
            print(f"✅ Default storage class: {stdout}")
        else:
            print("⚠️  No default storage class found")
            self.config["actions_needed"].append("set_default_storage_class")
        
        # Count persistent volumes
        success, stdout, _ = self._run_remote_cmd("microk8s kubectl get pv --no-headers | wc -l", timeout=10)
        if success and stdout.isdigit():
            storage_info["pv_count"] = int(stdout)
            print(f"📊 Found {storage_info['pv_count']} persistent volumes")
        
        return storage_info
    
    def generate_recommendations(self) -> List[str]:
        """Generate deployment recommendations based on current state."""
        recommendations = []
        
        # Check if full deployment is needed
        namespace_exists = self.config["namespaces"].get("azurephotoflow", {}).get("exists", False)
        deployments = self.config["deployments"]
        
        if not namespace_exists:
            recommendations.append("FULL_DEPLOYMENT: Namespace doesn't exist - full deployment needed")
        elif not any(d.get("exists", False) for d in deployments.values()):
            recommendations.append("FULL_DEPLOYMENT: No existing deployments - full deployment needed")
        else:
            # Check what needs updates
            ready_deployments = [name for name, info in deployments.items() if info.get("ready", False)]
            if len(ready_deployments) == len(deployments):
                recommendations.append("UPDATE_DEPLOYMENT: All deployments exist and ready - update images only")
            else:
                recommendations.append("PARTIAL_DEPLOYMENT: Some deployments missing or not ready")
        
        # Secret recommendations
        secrets = self.config["secrets"]
        if not secrets.get("azurephotoflow-secrets", False):
            recommendations.append("CREATE_SECRETS: Application secrets missing")
        if not secrets.get("registry-secret", False):
            recommendations.append("CREATE_REGISTRY_SECRET: Registry secret missing")
        
        # Addon recommendations
        if "enable_addons" in str(self.config["actions_needed"]):
            recommendations.append("ENABLE_ADDONS: Required addons missing")
        
        return recommendations
    
    def run_full_check(self) -> Dict:
        """Run complete cluster configuration check."""
        print("🚀 Starting cluster configuration check...")
        start_time = time.time()
        
        # Basic connectivity
        if not self.check_basic_connectivity():
            self.config["cluster_ready"] = False
            return self.config
        
        # MicroK8s status
        microk8s_status = self.check_microk8s_status()
        self.config["microk8s_status"] = microk8s_status
        
        if not microk8s_status["installed"]:
            self.config["cluster_ready"] = False
            return self.config
        
        # Addons
        self.config["addons"] = self.check_addons()
        
        # Namespace
        self.config["namespaces"]["azurephotoflow"] = self.check_namespace("azurephotoflow")
        
        # Secrets
        self.config["secrets"] = self.check_secrets("azurephotoflow")
        
        # Deployments
        self.config["deployments"] = self.check_deployments("azurephotoflow")
        
        # Storage
        self.config["storage"] = self.check_storage()
        
        # Generate recommendations
        self.config["recommendations"] = self.generate_recommendations()
        
        # Determine if cluster is ready
        self.config["cluster_ready"] = (
            microk8s_status["installed"] and 
            microk8s_status["api_responsive"] and
            len([a for a in self.config["actions_needed"] if "install" in a or "start" in a]) == 0
        )
        
        elapsed = time.time() - start_time
        print(f"✅ Configuration check completed in {elapsed:.1f}s")
        print(f"📊 Cluster ready: {self.config['cluster_ready']}")
        print(f"📋 Actions needed: {len(self.config['actions_needed'])}")
        print(f"💡 Recommendations: {len(self.config['recommendations'])}")
        
        return self.config
    
    def save_config(self, output_file: str):
        """Save configuration to JSON file for pipeline consumption."""
        with open(output_file, 'w') as f:
            json.dump(self.config, f, indent=2)
        print(f"💾 Configuration saved to {output_file}")

def main():
    import os
    
    # Get connection details from environment
    ssh_host = os.getenv('SSH_HOST', os.getenv('REMOTE_SSH_HOST', ''))
    ssh_user = os.getenv('SSH_USER', os.getenv('REMOTE_SSH_USER', ''))
    ssh_key = os.getenv('SSH_KEY', '')
    ssh_port = int(os.getenv('SSH_PORT', '22'))
    output_file = os.getenv('CONFIG_OUTPUT_FILE', 'cluster-config.json')
    
    if not ssh_host or not ssh_user:
        print("❌ Error: SSH_HOST and SSH_USER environment variables are required")
        sys.exit(1)
    
    print(f"🎯 Target: {ssh_user}@{ssh_host}:{ssh_port}")
    if ssh_key:
        print(f"🔑 Using SSH key: {ssh_key}")
    
    # Run configuration check
    checker = ClusterConfigChecker(ssh_host, ssh_user, ssh_key, ssh_port)
    config = checker.run_full_check()
    
    # Save results
    checker.save_config(output_file)
    
    # Print summary
    print("\n" + "="*60)
    print("📋 CLUSTER CONFIGURATION SUMMARY")
    print("="*60)
    
    if config["cluster_ready"]:
        print("✅ Cluster is ready for deployment")
    else:
        print("⚠️  Cluster needs preparation")
    
    if config["actions_needed"]:
        print("\n🔧 Actions needed:")
        for action in config["actions_needed"]:
            print(f"  - {action}")
    
    if config["recommendations"]:
        print("\n💡 Deployment recommendations:")
        for rec in config["recommendations"]:
            print(f"  - {rec}")
    
    # Exit with appropriate code
    sys.exit(0 if config["cluster_ready"] else 1)

if __name__ == "__main__":
    main()