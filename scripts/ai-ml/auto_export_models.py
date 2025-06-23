#!/usr/bin/env python3
"""
Automatically export the correct CLIP model based on .env configuration.
This script reads the .env file and ensures the matching ONNX models are available.
"""

import os
import sys
import argparse
import subprocess
from pathlib import Path
from typing import Dict, Optional

def load_env_file(env_path: str = ".env") -> Dict[str, str]:
    """Load environment variables from .env file."""
    env_vars = {}
    
    if not os.path.exists(env_path):
        print(f"⚠️  No .env file found at {env_path}")
        return env_vars
    
    with open(env_path, 'r') as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith('#') and '=' in line:
                key, value = line.split('=', 1)
                # Remove quotes if present
                value = value.strip('\'"')
                env_vars[key] = value
    
    return env_vars

def get_embedding_config(env_vars: Dict[str, str]) -> Dict[str, str]:
    """Extract embedding configuration from environment variables."""
    config = {
        'dimension': env_vars.get('EMBEDDING_DIMENSION', '512'),
        'variant': env_vars.get('EMBEDDING_MODEL_VARIANT', 'base'),
        'distance_metric': env_vars.get('EMBEDDING_DISTANCE_METRIC', 'Cosine')
    }
    
    # Validate configuration
    valid_variants = ['base', 'large', 'huge']
    if config['variant'] not in valid_variants:
        print(f"⚠️  Invalid model variant '{config['variant']}'. Using 'base' instead.")
        config['variant'] = 'base'
    
    # Auto-correct dimension based on variant if mismatch
    expected_dims = {'base': '512', 'large': '768', 'huge': '1024'}
    expected_dim = expected_dims[config['variant']]
    
    if config['dimension'] != expected_dim:
        print(f"🔧 Dimension mismatch: variant '{config['variant']}' expects {expected_dim}D, but config has {config['dimension']}D")
        print(f"🔧 Auto-correcting dimension to {expected_dim}")
        config['dimension'] = expected_dim
    
    return config

def check_models_exist(models_dir: str, config: Dict[str, str]) -> bool:
    """Check if the required models already exist and are valid."""
    models_path = Path(models_dir)
    
    vision_model = models_path / "vision_model.onnx"
    text_model = models_path / "text_model.onnx"
    tokenizer_dir = models_path / "tokenizer"
    
    if not all([vision_model.exists(), text_model.exists(), tokenizer_dir.exists()]):
        print(f"📋 Missing model files in {models_dir}")
        return False
    
    # Check if there's a model_info.txt file that tracks the current model variant
    info_file = models_path / "model_info.txt"
    if info_file.exists():
        try:
            with open(info_file, 'r') as f:
                content = f.read().strip()
                if f"variant={config['variant']}" in content and f"dimension={config['dimension']}" in content:
                    print(f"✅ Correct {config['variant']} model ({config['dimension']}D) already exists")
                    return True
        except Exception as e:
            print(f"⚠️  Could not read model info: {e}")
    
    print(f"🔄 Model variant mismatch or info missing. Need to export {config['variant']} model.")
    return False

def export_models(models_dir: str, config: Dict[str, str], force: bool = False) -> bool:
    """Export CLIP models using the existing export script."""
    if not force and check_models_exist(models_dir, config):
        return True
    
    print(f"📦 Exporting CLIP {config['variant']} model ({config['dimension']} dimensions)...")
    
    # Find the export script
    script_dir = Path(__file__).parent
    export_script = script_dir / "export_clip_onnx.py"
    
    if not export_script.exists():
        print(f"❌ Export script not found: {export_script}")
        return False
    
    try:
        # Run the export script
        cmd = [
            sys.executable, 
            str(export_script),
            "--variant", config['variant'],
            "--output", models_dir
        ]
        
        print(f"🚀 Running: {' '.join(cmd)}")
        result = subprocess.run(cmd, check=True, capture_output=True, text=True)
        
        print("✅ Model export completed successfully!")
        print(result.stdout)
        
        # Create model info file to track what we exported
        info_file = Path(models_dir) / "model_info.txt"
        with open(info_file, 'w') as f:
            f.write(f"variant={config['variant']}\n")
            f.write(f"dimension={config['dimension']}\n")
            f.write(f"distance_metric={config['distance_metric']}\n")
            f.write(f"exported_by=auto_export_models.py\n")
        
        return True
        
    except subprocess.CalledProcessError as e:
        print(f"❌ Model export failed: {e}")
        print(f"stdout: {e.stdout}")
        print(f"stderr: {e.stderr}")
        return False
    except Exception as e:
        print(f"❌ Unexpected error during export: {e}")
        return False

def update_env_file(env_path: str, config: Dict[str, str]) -> None:
    """Update .env file with corrected configuration if needed."""
    env_vars = load_env_file(env_path)
    
    updated = False
    if env_vars.get('EMBEDDING_DIMENSION') != config['dimension']:
        env_vars['EMBEDDING_DIMENSION'] = config['dimension']
        updated = True
    
    if updated:
        print(f"🔧 Updating {env_path} with corrected configuration...")
        
        # Read the original file to preserve formatting and comments
        with open(env_path, 'r') as f:
            lines = f.readlines()
        
        # Update the relevant lines
        with open(env_path, 'w') as f:
            for line in lines:
                if line.strip().startswith('EMBEDDING_DIMENSION='):
                    f.write(f"EMBEDDING_DIMENSION={config['dimension']}\n")
                else:
                    f.write(line)

def main():
    parser = argparse.ArgumentParser(description="Auto-export CLIP models based on .env configuration")
    parser.add_argument("--env-file", default=".env", help="Path to .env file (default: .env)")
    parser.add_argument("--models-dir", default="models", help="Directory to export models to (default: models)")
    parser.add_argument("--force", action="store_true", help="Force re-export even if models exist")
    parser.add_argument("--check-only", action="store_true", help="Only check configuration, don't export")
    parser.add_argument("--update-env", action="store_true", help="Update .env file with corrected values")
    
    args = parser.parse_args()
    
    print("🤖 AzurePhotoFlow Auto Model Export")
    print("=" * 40)
    
    # Load environment configuration
    env_vars = load_env_file(args.env_file)
    if not env_vars:
        print(f"❌ Could not load environment from {args.env_file}")
        sys.exit(1)
    
    # Get embedding configuration
    config = get_embedding_config(env_vars)
    print(f"📋 Configuration from {args.env_file}:")
    print(f"   • Model Variant: {config['variant']}")
    print(f"   • Embedding Dimension: {config['dimension']}")
    print(f"   • Distance Metric: {config['distance_metric']}")
    print()
    
    # Update .env file if requested and corrections were made
    if args.update_env:
        update_env_file(args.env_file, config)
    
    # Check if models exist
    models_exist = check_models_exist(args.models_dir, config)
    
    if args.check_only:
        if models_exist:
            print("✅ All required models are available and match configuration")
            sys.exit(0)
        else:
            print("❌ Models missing or don't match configuration")
            sys.exit(1)
    
    # Create models directory if it doesn't exist
    os.makedirs(args.models_dir, exist_ok=True)
    
    # Export models if needed
    success = export_models(args.models_dir, config, args.force)
    
    if success:
        print(f"\n🎉 Ready to use {config['variant']} CLIP model with {config['dimension']} dimensions!")
        print(f"📁 Models available in: {os.path.abspath(args.models_dir)}")
        sys.exit(0)
    else:
        print("\n❌ Model export failed!")
        sys.exit(1)

if __name__ == "__main__":
    main()