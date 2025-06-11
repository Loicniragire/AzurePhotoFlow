#!/usr/bin/env python3
"""Utility to export a CLIP vision model to ONNX."""

import argparse

import torch
from transformers import CLIPModel


def export_clip_model(output_path: str, model_name: str = "openai/clip-vit-base-patch32"):
    """Export the vision part of a CLIP model to ONNX."""
    model = CLIPModel.from_pretrained(model_name)

    dummy_input = torch.zeros((1, 3, 224, 224), dtype=torch.float32)

    torch.onnx.export(
        model.vision_model,
        dummy_input,
        output_path,
        input_names=["input"],
        output_names=["output"],
        dynamic_axes={"input": {0: "batch"}, "output": {0: "batch"}},
        opset_version=14,
    )
    print(f"Model exported to {output_path}")


def main():
    parser = argparse.ArgumentParser(description="Export CLIP model to ONNX")
    parser.add_argument("--model", default="openai/clip-vit-base-patch32", help="HuggingFace model name")
    parser.add_argument("--output", default="models/model.onnx", help="Output path for ONNX model")
    args = parser.parse_args()
    export_clip_model(args.output, args.model)


if __name__ == "__main__":
    main()
