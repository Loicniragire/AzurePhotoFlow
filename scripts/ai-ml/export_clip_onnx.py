#!/usr/bin/env python3
"""Utility to export CLIP vision and text models to ONNX."""

import argparse
import os
import importlib.util

import torch
from transformers import CLIPModel, CLIPTokenizer


os.environ["HF_HOME"] = "./.hf_cache"


def export_clip_model(output_dir: str, model_name: str = "openai/clip-vit-base-patch32"):
    """Export both vision and text parts of a CLIP model to ONNX."""
    if importlib.util.find_spec("onnx") is None:
        raise RuntimeError(
            "onnx package is required to export the model. Install it via 'pip install onnx'."
        )
    print("📥 Loading CLIP model...")
    # Using the "eager" attention implementation avoids PyTorch's
    # scaled_dot_product_attention operator which currently fails
    # during ONNX export.
    model = CLIPModel.from_pretrained(
        model_name,
        use_safetensors=True,
        attn_implementation="eager",
    )
    model.eval()

    os.makedirs(output_dir, exist_ok=True)

    # Export Vision Model
    class VisionWrapper(torch.nn.Module):
        def __init__(self, clip_model):
            super().__init__()
            self.clip_model = clip_model

        def forward(self, pixel_values):
            return self.clip_model.get_image_features(pixel_values)

    vision_wrapper = VisionWrapper(model)
    vision_dummy_input = torch.zeros((1, 3, 224, 224), dtype=torch.float32)

    vision_output_path = os.path.join(output_dir, "vision_model.onnx")
    print("📤 Exporting vision model to ONNX...")

    torch.onnx.export(
        vision_wrapper,
        vision_dummy_input,
        vision_output_path,
        input_names=["input"],
        output_names=["output"],
        dynamic_axes={"input": {0: "batch"}, "output": {0: "batch"}},
        opset_version=14,
    )

    print(f"Vision model exported to {vision_output_path}")

    # Export Text Model
    class TextWrapper(torch.nn.Module):
        def __init__(self, clip_model):
            super().__init__()
            self.clip_model = clip_model

        def forward(self, input_ids, attention_mask):
            return self.clip_model.get_text_features(input_ids=input_ids, attention_mask=attention_mask)

    text_wrapper = TextWrapper(model)
    
    # Create dummy text inputs (max length 77 for CLIP)
    text_dummy_input_ids = torch.zeros((1, 77), dtype=torch.long)
    text_dummy_attention_mask = torch.ones((1, 77), dtype=torch.long)

    text_output_path = os.path.join(output_dir, "text_model.onnx")
    print("📤 Exporting text model to ONNX...")

    torch.onnx.export(
        text_wrapper,
        (text_dummy_input_ids, text_dummy_attention_mask),
        text_output_path,
        input_names=["input_ids", "attention_mask"],
        output_names=["output"],
        dynamic_axes={
            "input_ids": {0: "batch", 1: "sequence"}, 
            "attention_mask": {0: "batch", 1: "sequence"},
            "output": {0: "batch", 1: "sequence"}
        },
        opset_version=14,
    )

    print(f"Text model exported to {text_output_path}")

    # Save tokenizer for text processing
    tokenizer = CLIPTokenizer.from_pretrained(model_name)
    tokenizer_path = os.path.join(output_dir, "tokenizer")
    tokenizer.save_pretrained(tokenizer_path)
    print(f"Tokenizer saved to {tokenizer_path}")

    # Create backward compatibility symlink for vision model
    legacy_path = os.path.join(output_dir, "model.onnx")
    if not os.path.exists(legacy_path):
        os.symlink("vision_model.onnx", legacy_path)
        print(f"Created backward compatibility symlink: {legacy_path}")

    print("✅ CLIP model export complete!")

def main():
    parser = argparse.ArgumentParser(description="Export CLIP model to ONNX")
    parser.add_argument("--model", default="openai/clip-vit-base-patch32", help="HuggingFace model name")
    parser.add_argument("--output", default="models", help="Output directory for ONNX models")
    args = parser.parse_args()
    export_clip_model(args.output, args.model)


if __name__ == "__main__":
    main()
