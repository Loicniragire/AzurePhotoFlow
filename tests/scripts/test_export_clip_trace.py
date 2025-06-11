import tempfile
from unittest import mock

import importlib.util
import os
import sys

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
EXPORT_PATH = os.path.join(ROOT_DIR, "scripts", "export_clip_trace.py")

spec = importlib.util.spec_from_file_location("scripts.export_clip_trace", EXPORT_PATH)
exp = importlib.util.module_from_spec(spec)
with mock.patch.dict(sys.modules, {"torch": mock.Mock(), "transformers": mock.Mock()}):
    assert spec.loader is not None
    spec.loader.exec_module(exp)


def test_export_calls_torch_trace():
    with tempfile.NamedTemporaryFile() as tmp:
        with mock.patch.object(exp, "CLIPModel") as mock_model_cls, \
             mock.patch.object(exp, "torch") as mock_torch:
            model_instance = mock.Mock()
            model_instance.vision_model = object()
            mock_model_cls.from_pretrained.return_value = model_instance

            trace_result = mock.Mock()
            mock_torch.jit.trace.return_value = trace_result

            exp.export_clip_model(tmp.name, model_name="a/b")

            mock_model_cls.from_pretrained.assert_called_with("a/b", use_safetensors=True)
            assert mock_torch.jit.trace.called
            assert trace_result.save.called

