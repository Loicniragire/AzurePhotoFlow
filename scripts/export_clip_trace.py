#!/usr/bin/env python3
"""Utility to export a CLIP vision model to TorchScript (.pt) using tracing."""

import argparse
import os

# ✅ Use local Hugging Face cache to avoid permission issues
os.environ["HF_HOME"] = "./.hf_cache"

import torch
from transformers import CLIPModel

def export_clip_model(output_path: str, model_name: str = "openai/clip-vit-base-patch32"):
    print("📥 Loading CLIP model...")
    model = CLIPModel.from_pretrained(model_name, use_safetensors=True)
    model.eval()

    vision_model = model.vision_model

    class VisionWrapper(torch.nn.Module):
        def __init__(self, vision_model):
            super().__init__()
            self.vision_model = vision_model

        def forward(self, pixel_values):
            return self.vision_model(pixel_values).last_hidden_state

    dummy_input = torch.zeros((1, 3, 224, 224), dtype=torch.float32)
    wrapper = VisionWrapper(vision_model)

    print("🎥 Tracing the vision model...")
    traced = torch.jit.trace(wrapper, dummy_input)

    # ✅ Ensure output directory exists
    os.makedirs(os.path.dirname(output_path), exist_ok=True)

    print(f"💾 Saving traced model to: {output_path}")
    traced.save(output_path)

    print("✅ Export complete (TorchScript format).")

def main():
    parser = argparse.ArgumentParser(description="Export CLIP vision encoder to TorchScript")
    parser.add_argument("--model", default="openai/clip-vit-base-patch32", help="HuggingFace model name")
    parser.add_argument("--output", default="models/clip_vision_traced.pt", help="Output path for TorchScript model")
    args = parser.parse_args()
    export_clip_model(args.output, args.model)


if __name__ == "__main__":
    main()

