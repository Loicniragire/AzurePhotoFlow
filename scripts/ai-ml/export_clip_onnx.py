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

    # Create model info file
    info_path = os.path.join(output_dir, "model_info.txt")
    with open(info_path, "w") as f:
        f.write(f"model_name={model_name}\n")
        f.write(f"tokenizer_type=CLIPTokenizer\n")
        f.write(f"tokenizer_method=BPE\n")
        f.write(f"max_tokens=77\n")
        f.write(f"vocab_size={tokenizer.vocab_size}\n")
        f.write(f"exports=vision_model.onnx,text_model.onnx,tokenizer/\n")
    print(f"Model info saved to {info_path}")

    # Create backward compatibility symlink for vision model
    legacy_path = os.path.join(output_dir, "model.onnx")
    if not os.path.exists(legacy_path):
        os.symlink("vision_model.onnx", legacy_path)
        print(f"Created backward compatibility symlink: {legacy_path}")

    print("✅ CLIP model export complete!")
    
    # Validate the exported models
    validate_exported_models(output_dir)

def validate_exported_models(output_dir: str):
    """Validate that exported ONNX models have correct output dimensions."""
    try:
        import onnxruntime as ort
        import numpy as np
        
        vision_path = os.path.join(output_dir, "vision_model.onnx")
        text_path = os.path.join(output_dir, "text_model.onnx")
        
        if os.path.exists(vision_path):
            print("🔍 Validating vision model...")
            vision_session = ort.InferenceSession(vision_path)
            dummy_image = np.random.randn(1, 3, 224, 224).astype(np.float32)
            vision_outputs = vision_session.run(None, {"input": dummy_image})
            vision_dims = vision_outputs[0].shape[-1]
            print(f"✅ Vision model output dimensions: {vision_dims}")
            
        if os.path.exists(text_path):
            print("🔍 Validating text model...")
            text_session = ort.InferenceSession(text_path)
            dummy_input_ids = np.zeros((1, 77), dtype=np.int64)
            dummy_attention_mask = np.ones((1, 77), dtype=np.int64)
            text_outputs = text_session.run(None, {
                "input_ids": dummy_input_ids,
                "attention_mask": dummy_attention_mask
            })
            text_dims = text_outputs[0].shape[-1]
            print(f"✅ Text model output dimensions: {text_dims}")
            
            # Verify both models have matching dimensions
            if os.path.exists(vision_path) and vision_dims == text_dims:
                print(f"✅ Model validation successful: Both models output {vision_dims}-dimensional embeddings")
            elif os.path.exists(vision_path):
                print(f"⚠️  Dimension mismatch: Vision={vision_dims}, Text={text_dims}")
                
    except ImportError:
        print("⚠️  ONNX Runtime not available for validation. Install with: pip install onnxruntime")
    except Exception as e:
        print(f"⚠️  Validation error: {e}")

def main():
    parser = argparse.ArgumentParser(description="Export CLIP model to ONNX")
    parser.add_argument("--model", default="openai/clip-vit-base-patch32", help="HuggingFace model name")
    parser.add_argument("--variant", choices=["base", "large", "huge"], help="Model variant (overrides --model)")
    parser.add_argument("--output", default="models", help="Output directory for ONNX models")
    args = parser.parse_args()
    
    # Map variant to model name
    if args.variant:
        variant_models = {
            "base": "openai/clip-vit-base-patch32",      # 512 dimensions
            "large": "openai/clip-vit-large-patch14",    # 768 dimensions  
            "huge": "laion/CLIP-ViT-H-14-laion2B-s32B-b79K"  # 1024 dimensions
        }
        model_name = variant_models[args.variant]
        print(f"🎯 Using model variant '{args.variant}': {model_name}")
        
        # Get expected dimensions for validation
        expected_dims = {"base": 512, "large": 768, "huge": 1024}
        print(f"📏 Expected embedding dimensions: {expected_dims[args.variant]}")
    else:
        model_name = args.model
        print(f"🎯 Using custom model: {model_name}")
    
    export_clip_model(args.output, model_name)


if __name__ == "__main__":
    main()
