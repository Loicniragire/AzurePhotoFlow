import importlib.util
import os
from unittest import mock

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SCRIPT_PATH = os.path.join(ROOT_DIR, "scripts", "check-cluster-config.py")

spec = importlib.util.spec_from_file_location("scripts.check_cluster_config", SCRIPT_PATH)
ccc = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(ccc)


def test_check_addons_json_format():
    checker = ccc.ClusterConfigChecker("host", "user")
    json_output = (
        '{"microk8s": "running", "addons": {"enabled": ["dns", "storage", "ingress"], "disabled": ["registry"]}}'
    )
    with mock.patch.object(checker, "_run_remote_cmd", return_value=(True, json_output, "")) as mock_run:
        addons = checker.check_addons()
        mock_run.assert_called_with("microk8s status --format json", timeout=15)
    assert addons["dns"] == "enabled"
    assert addons["storage"] == "enabled"
    assert addons["ingress"] == "enabled"
    assert addons["registry"] == "disabled"


def test_check_addons_legacy_format():
    checker = ccc.ClusterConfigChecker("host", "user")
    legacy_output = "dns: enabled\nstorage: disabled\ningress: enabled"
    with mock.patch.object(checker, "_run_remote_cmd", return_value=(True, legacy_output, "")):
        addons = checker.check_addons()
    assert addons["dns"] == "enabled"
    assert addons["storage"] == "disabled"
    assert addons["ingress"] == "enabled"
