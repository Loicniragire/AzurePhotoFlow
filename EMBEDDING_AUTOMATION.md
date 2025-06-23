# Automated Embedding Model Management

AzurePhotoFlow now automatically manages CLIP model exports based on your `.env` configuration. No more manual model export steps!

## 🚀 Quick Start

### Option 1: Complete Automated Setup
```bash
# One command setup - handles everything automatically
make setup
```

### Option 2: Manual Steps
```bash
# 1. Configure your desired embedding dimension in .env
echo "EMBEDDING_DIMENSION=768" >> .env
echo "EMBEDDING_MODEL_VARIANT=large" >> .env

# 2. Auto-export the correct model
python3 scripts/ai-ml/auto_export_models.py

# 3. Start the application
make dev
```

## 📋 How It Works

The automation system:

1. **Reads your `.env` file** to understand your embedding preferences
2. **Validates configuration** and auto-corrects mismatches
3. **Checks existing models** to see if they match your configuration
4. **Exports new models** only when needed (saves time!)
5. **Tracks model info** to avoid unnecessary re-exports

## 🎛️ Configuration Options

### In your `.env` file:
```bash
# Embedding dimension (affects accuracy vs speed)
EMBEDDING_DIMENSION=512   # Options: 512, 768, 1024

# Model variant (must match dimension)
EMBEDDING_MODEL_VARIANT=base  # Options: base, large, huge

# Distance metric for similarity search
EMBEDDING_DISTANCE_METRIC=Cosine  # Options: Cosine, Dot, Euclidean
```

### Model Variants:
- **base** (512D): Fast, good for development and demos
- **large** (768D): Better accuracy, moderate resource usage
- **huge** (1024D): Best accuracy, highest resource requirements

## 🛠️ Available Commands

### Makefile Commands (Recommended)
```bash
make setup           # Complete automated setup
make dev            # Start development with auto model check
make models         # Update models based on .env
make check-models   # Verify models match configuration

# Quick model switching:
make models-512     # Switch to base model (512D)
make models-768     # Switch to large model (768D)
make models-1024    # Switch to huge model (1024D)

# Deploy with specific model:
make deploy-512     # Deploy with base model
make deploy-768     # Deploy with large model
make deploy-1024    # Deploy with huge model
```

### Script Commands
```bash
# Auto export based on .env
python3 scripts/ai-ml/auto_export_models.py

# Check if models match configuration
python3 scripts/ai-ml/auto_export_models.py --check-only

# Force re-export even if models exist
python3 scripts/ai-ml/auto_export_models.py --force

# Update .env file with corrected values
python3 scripts/ai-ml/auto_export_models.py --update-env
```

## 🔄 Typical Workflow

### Daily Development
```bash
# Just start developing - models auto-managed
make dev
```

### Changing Model Size
```bash
# Method 1: Edit .env manually
vim .env  # Change EMBEDDING_DIMENSION=768

# Method 2: Use shortcuts
make models-768

# Then restart
make dev
```

### Experimenting with Different Models
```bash
# Try base model
make deploy-512

# Try large model for better accuracy
make deploy-768

# Try huge model for best accuracy
make deploy-1024
```

## 📁 Generated Files

The automation creates these files:

```
models/
├── vision_model.onnx          # CLIP vision encoder
├── text_model.onnx           # CLIP text encoder  
├── model.onnx               # Backward compatibility symlink
├── model_info.txt           # Tracks current model variant
└── tokenizer/               # CLIP tokenizer files
    ├── vocab.json
    ├── merges.txt
    └── ...
```

## ⚠️ Important Notes

- **Existing embeddings become incompatible** when you change dimensions
- **Qdrant collections are recreated** automatically with new dimensions
- **Re-upload images** after dimension changes to generate new embeddings
- **Larger models require more memory** (especially 1024D model)

## 🔧 Troubleshooting

### Models not found
```bash
# Force re-export
python3 scripts/ai-ml/auto_export_models.py --force
```

### Configuration mismatch
```bash
# Auto-fix .env file
python3 scripts/ai-ml/auto_export_models.py --update-env
```

### Python dependencies missing
```bash
# Complete setup installs everything
make setup
```

### Check current status
```bash
make config        # Show .env configuration
make check-models  # Verify model status
```

## 🎯 Benefits

✅ **No manual model export** - automatically handled  
✅ **Configuration validation** - prevents mismatches  
✅ **Intelligent caching** - only exports when needed  
✅ **Easy experimentation** - switch models with one command  
✅ **Docker integration** - works in containers  
✅ **Development friendly** - simple make commands  

Start using automated model management today with `make setup`! 🚀