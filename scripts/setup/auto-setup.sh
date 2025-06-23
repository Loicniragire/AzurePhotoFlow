#!/bin/bash
# Auto-setup script for AzurePhotoFlow
# Ensures the correct CLIP models are exported based on .env configuration

set -e

echo "🚀 AzurePhotoFlow Auto-Setup"
echo "=============================="

# Check if we're in the project root
if [ ! -f ".env" ] || [ ! -f "docker-compose.yml" ]; then
    echo "❌ Please run this script from the AzurePhotoFlow project root directory"
    echo "   Expected files: .env, docker-compose.yml"
    exit 1
fi

# Check if Python is available
PYTHON_CMD="python3"
if ! command -v $PYTHON_CMD &> /dev/null; then
    PYTHON_CMD="python"
    if ! command -v $PYTHON_CMD &> /dev/null; then
        echo "❌ Python not found. Please install Python 3.8+"
        echo "   On macOS: brew install python"
        echo "   On Ubuntu: sudo apt install python3 python3-pip"
        exit 1
    fi
fi

echo "✅ Found Python: $($PYTHON_CMD --version)"

# Check if virtual environment exists, create if not
VENV_DIR="venv"
if [ ! -d "$VENV_DIR" ]; then
    echo "📦 Creating Python virtual environment..."
    $PYTHON_CMD -m venv $VENV_DIR
fi

# Activate virtual environment
echo "🔧 Activating virtual environment..."
source $VENV_DIR/bin/activate

# Install/upgrade required packages
echo "📥 Installing Python dependencies..."
pip install --upgrade pip
pip install torch torchvision --index-url https://download.pytorch.org/whl/cpu
pip install transformers onnx onnxruntime

# Run the auto model export
echo "🤖 Running auto model export..."
$PYTHON_CMD scripts/ai-ml/auto_export_models.py --update-env

# Check Docker
if command -v docker &> /dev/null && command -v docker-compose &> /dev/null; then
    echo "✅ Docker and Docker Compose found"
    
    # Ask if user wants to start the application
    read -p "🚀 Start AzurePhotoFlow with Docker Compose? (y/n): " -r
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo "🐳 Starting AzurePhotoFlow..."
        docker-compose up --build
    else
        echo "✅ Setup complete! You can now run: docker-compose up --build"
    fi
else
    echo "⚠️  Docker not found. Please install Docker and Docker Compose to run the application."
    echo "✅ Model setup complete! Models are ready in ./models/"
fi

echo ""
echo "🎉 AzurePhotoFlow setup complete!"
echo ""
echo "📋 Configuration Summary:"
echo "   • Models directory: ./models/"
echo "   • Environment: .env"
echo "   • Virtual environment: ./venv/"
echo ""
echo "💡 To change model dimensions:"
echo "   1. Edit EMBEDDING_DIMENSION in .env (512, 768, or 1024)"
echo "   2. Run: ./scripts/setup/auto-setup.sh"
echo "   3. Restart: docker-compose up --build"