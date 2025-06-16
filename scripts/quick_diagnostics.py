import os
import subprocess
import sys

SSH_OPTIONS = [
    "-o", "StrictHostKeyChecking=no",
    "-o", "UserKnownHostsFile=/dev/null",
    "-o", "ConnectTimeout=5",
]


def build_ssh_command(command: str) -> list[str]:
    user = os.environ.get("SSH_USER")
    host = os.environ.get("SSH_HOST")
    if not user or not host:
        raise RuntimeError("SSH_USER and SSH_HOST must be set")
    port = os.environ.get("SSH_PORT", "22")
    key = os.environ.get("SSH_KEY")

    cmd = ["ssh"]
    if key:
        cmd += ["-i", key]
    cmd += SSH_OPTIONS + ["-p", str(port), f"{user}@{host}", command]
    return cmd


def run(command: list[str]) -> subprocess.CompletedProcess:
    return subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE)


def microk8s_ready(timeout: int = 15) -> bool:
    cmd = build_ssh_command(f"microk8s status --wait-ready --timeout={timeout}")
    result = run(cmd)
    return result.returncode == 0


def restart_microk8s() -> bool:
    run(build_ssh_command("microk8s stop && microk8s start"))
    return microk8s_ready(60)


def run_quick_diagnostics() -> int:
    print("Running quick diagnostics...")
    if microk8s_ready():
        print("MicroK8s is ready")
        return 0

    print("MicroK8s not ready, attempting restart...")
    if restart_microk8s():
        print("MicroK8s restarted successfully")
        return 0

    print("MicroK8s failed to start", file=sys.stderr)
    return 1


def main() -> None:
    sys.exit(run_quick_diagnostics())


if __name__ == "__main__":
    main()
