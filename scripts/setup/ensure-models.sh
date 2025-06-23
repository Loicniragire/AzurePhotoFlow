#!/bin/bash
# Ensure the correct CLIP models are available before starting the application

set -e

echo "🔍 Checking CLIP model requirements..."

# Default paths
ENV_FILE="${ENV_FILE:-.env}"
MODELS_DIR="${MODELS_DIR:-models}"
PYTHON_CMD="${PYTHON_CMD:-python3}"

# Check if we're in a container or development environment
if [ -f "/.dockerenv" ]; then
    echo "🐳 Running in Docker container"
    CONTEXT="container"
else
    echo "💻 Running in development environment"
    CONTEXT="development"
fi

# Check if Python is available
if ! command -v $PYTHON_CMD &> /dev/null; then
    echo "❌ Python not found. Please install Python 3.8+ or set PYTHON_CMD environment variable."
    exit 1
fi

# Check if the auto export script exists
AUTO_EXPORT_SCRIPT="scripts/ai-ml/auto_export_models.py"
if [ ! -f "$AUTO_EXPORT_SCRIPT" ]; then
    echo "❌ Auto export script not found: $AUTO_EXPORT_SCRIPT"
    exit 1
fi

# Install Python dependencies if needed
echo "📦 Checking Python dependencies..."
if ! $PYTHON_CMD -c "import torch, transformers" &> /dev/null; then
    echo "📥 Installing required Python packages..."
    if [ "$CONTEXT" = "container" ]; then
        # In container, install minimal dependencies
        pip install --no-cache-dir torch torchvision --index-url https://download.pytorch.org/whl/cpu
        pip install --no-cache-dir transformers onnx
    else
        # In development, use requirements.txt if available
        if [ -f "requirements.txt" ]; then
            pip install -r requirements.txt
        else
            pip install torch torchvision transformers onnx
        fi
    fi
fi

# Run the auto export script
echo "🚀 Running auto model export..."
$PYTHON_CMD "$AUTO_EXPORT_SCRIPT" \
    --env-file "$ENV_FILE" \
    --models-dir "$MODELS_DIR" \
    --update-env

# Verify models are ready
if $PYTHON_CMD "$AUTO_EXPORT_SCRIPT" --check-only --env-file "$ENV_FILE" --models-dir "$MODELS_DIR"; then
    echo "✅ CLIP models are ready!"
else
    echo "❌ CLIP models are not ready!"
    exit 1
fi

echo "🎉 Model setup complete!"