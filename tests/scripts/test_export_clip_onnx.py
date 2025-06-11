import tempfile
from unittest import mock

import importlib.util
import os
import sys

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
EXPORT_PATH = os.path.join(ROOT_DIR, "scripts", "export_clip_onnx.py")

spec = importlib.util.spec_from_file_location("scripts.export_clip_onnx", EXPORT_PATH)
exp = importlib.util.module_from_spec(spec)
with mock.patch.dict(sys.modules, {"torch": mock.Mock(), "transformers": mock.Mock()}):
    assert spec.loader is not None
    spec.loader.exec_module(exp)


def test_export_calls_torch_export():
    with tempfile.NamedTemporaryFile() as tmp:
        with mock.patch.object(exp, "CLIPModel") as mock_model_cls, \
             mock.patch.object(exp, "torch") as mock_torch, \
             mock.patch.object(exp.os, "makedirs") as mock_makedirs:
            model_instance = mock.Mock()
            model_instance.vision_model = object()
            mock_model_cls.from_pretrained.return_value = model_instance

            exp.export_clip_model(tmp.name, model_name="a/b")

            mock_model_cls.from_pretrained.assert_called_with(
                "a/b", use_safetensors=True, attn_implementation="eager"
            )
            assert mock_torch.onnx.export.called
            assert mock_makedirs.called

