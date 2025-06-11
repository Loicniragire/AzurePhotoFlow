import importlib.util
import os
import sys
from pathlib import Path
from unittest import mock
import tempfile

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SETUP_PATH = os.path.join(ROOT_DIR, "scripts", "setup_venv.py")

spec = importlib.util.spec_from_file_location("scripts.setup_venv", SETUP_PATH)
setup_mod = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(setup_mod)


def test_create_venv_runs_expected_commands():
    with tempfile.TemporaryDirectory() as tmpdir:
        with mock.patch.object(setup_mod.subprocess, "check_call") as mock_call, \
             mock.patch.object(setup_mod.shutil, "which", return_value=sys.executable):
            setup_mod.create_venv(tmpdir, "req.txt")

            pip = Path(tmpdir) / ("Scripts" if os.name == "nt" else "bin") / "pip"
        expected_calls = [
            mock.call([sys.executable, "-m", "venv", str(tmpdir)]),
            mock.call([str(pip), "install", "--upgrade", "pip"]),
            mock.call([str(pip), "install", "torch>=2.1.0", "--extra-index-url", "https://download.pytorch.org/whl/cpu"]),
        ]
        assert mock_call.mock_calls == expected_calls
