import argparse
import os
import subprocess
import sys
from pathlib import Path


def create_venv(path: str = "venv", requirements: str = "requirements.txt") -> None:
    """Create a Python virtual environment and install dependencies.

    Parameters
    ----------
    path : str, optional
        Directory where the virtual environment will be created.
    requirements : str, optional
        Path to the requirements file to install.
    """
    venv_path = Path(path)
    subprocess.check_call([sys.executable, "-m", "venv", str(venv_path)])
    pip_executable = venv_path / ("Scripts" if os.name == "nt" else "bin") / "pip"
    subprocess.check_call([str(pip_executable), "install", "-r", requirements])
    print(f"Virtual environment created at {venv_path}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Setup Python virtual environment")
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
