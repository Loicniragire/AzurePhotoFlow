import importlib.util
import os
from unittest import mock

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SCRIPT_PATH = os.path.join(ROOT_DIR, "scripts", "quick_diagnostics.py")

spec = importlib.util.spec_from_file_location("scripts.quick_diagnostics", SCRIPT_PATH)
qd = importlib.util.module_from_spec(spec)
spec.loader.exec_module(qd)  # type: ignore


def _mock_proc(returncode=0):
    proc = mock.Mock()
    proc.returncode = returncode
    return proc


def test_ready_returns_zero_without_restart():
    os.environ["SSH_USER"] = "user"
    os.environ["SSH_HOST"] = "host"
    with mock.patch.object(qd.subprocess, "run", return_value=_mock_proc(0)) as mock_run:
        assert qd.run_quick_diagnostics() == 0
        assert mock_run.call_count == 1
        assert "--timeout=15" in " ".join(mock_run.call_args_list[0][0][0])


def test_restart_when_not_ready():
    os.environ["SSH_USER"] = "user"
    os.environ["SSH_HOST"] = "host"
    mock_run = mock.Mock(side_effect=[_mock_proc(1), _mock_proc(0), _mock_proc(0)])
    with mock.patch.object(qd.subprocess, "run", mock_run):
        assert qd.run_quick_diagnostics() == 0
        assert mock_run.call_count == 3
        # Check commands
        first_cmd = " ".join(mock_run.call_args_list[0][0][0])
        second_cmd = " ".join(mock_run.call_args_list[1][0][0])
        third_cmd = " ".join(mock_run.call_args_list[2][0][0])
        assert "microk8s status" in first_cmd
        assert "microk8s stop" in second_cmd
        assert "--timeout=60" in third_cmd
