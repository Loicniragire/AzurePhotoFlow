import argparse
import os
import subprocess
import sys
from pathlib import Path
import shutil


def create_venv(path: str = "venv", requirements: str = "requirements.txt") -> None:
    """Create a Python 3.11 virtual environment and install dependencies."""
    venv_path = Path(path)

    # Locate python3.11 in system PATH
    python_exe = shutil.which("python3.11")
    if not python_exe:
        sys.exit(
            "❌ Python 3.11 not found in PATH.\n"
            "➡️  Install it with: brew install python@3.11\n"
            "📌 And add to your shell config: export PATH=\"/opt/homebrew/opt/python@3.11/bin:$PATH\""
        )

    print(f"📦 Creating virtual environment at: {venv_path}")
    subprocess.check_call([python_exe, "-m", "venv", str(venv_path)])

    pip_executable = venv_path / ("Scripts" if os.name == "nt" else "bin") / "pip"

    # Upgrade pip first
    subprocess.check_call([str(pip_executable), "install", "--upgrade", "pip"])

    # Install latest safe version of torch
    print("📥 Installing PyTorch >= 2.1.0 (CPU version)...")
    subprocess.check_call([
        str(pip_executable),
        "install",
        "torch>=2.1.0",
        "--extra-index-url", "https://download.pytorch.org/whl/cpu"
    ])

    # Install other requirements
    if Path(requirements).is_file():
        print(f"📥 Installing additional dependencies from {requirements}...")
        subprocess.check_call([str(pip_executable), "install", "-r", requirements])
    else:
        print(f"⚠️  No requirements.txt found at: {requirements}")

    print(f"✅ Virtual environment setup complete at: {venv_path}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Setup Python 3.11 virtual environment")
    parser.add_argument("--path", default="venv", help="Directory for the virtual environment")
    parser.add_argument(
        "--requirements",
        default="requirements.txt",
        help="Path to requirements.txt",
    )
    args = parser.parse_args()
    create_venv(args.path, args.requirements)


if __name__ == "__main__":
    main()
