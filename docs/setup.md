# Development Setup Guide

This guide will help you set up AzurePhotoFlow for local development.

## Prerequisites

### Required Software
- [.NET SDK 8.0+](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18.x/20.x LTS](https://nodejs.org/en/download/)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/)
- [Python 3.8+](https://www.python.org/downloads/) (for AI model export)
- [Git](https://git-scm.com/downloads)

### Optional Tools
- [kubectl](https://kubernetes.io/docs/tasks/tools/) (for Kubernetes deployment)
- [Visual Studio Code](https://code.visualstudio.com/) with C# and React extensions

## Quick Start

### 1. Clone Repository
```bash
git clone https://github.com/Loicniragire/AzurePhotoFlow.git
cd AzurePhotoFlow
```

### 2. Environment Setup

Copy and configure environment variables:
```bash
# Copy environment template
cp .env.development .env

# Edit with your configuration
vim .env
```

Required environment variables:
```bash
# Google OAuth
VITE_GOOGLE_CLIENT_ID=your-google-client-id

# JWT Secret (generate a secure random string)
JWT_SECRET_KEY=your-jwt-secret-key

# MinIO Configuration
MINIO_ENDPOINT=localhost:9000
MINIO_ACCESS_KEY=minioadmin
MINIO_SECRET_KEY=minioadmin

# Qdrant Configuration
QDRANT_URL=localhost:6333
QDRANT_COLLECTION=test

# AI Model
CLIP_MODEL_PATH=/models/model.onnx
ENABLE_EMBEDDINGS=true

# CORS
ALLOWED_ORIGINS=http://localhost:80
```

### 3. AI Model Setup

The backend requires an ONNX version of the CLIP vision model. Set up the Python environment and export the model:

```bash
# Create Python virtual environment
python scripts/ai-ml/setup_venv.py --path .venv

# Activate environment
source .venv/bin/activate  # Linux/macOS
# .venv\Scripts\activate   # Windows

# Export CLIP model to ONNX format
python scripts/ai-ml/export_clip_onnx.py --output models/model.onnx
```

This downloads the pre-trained CLIP model and converts it to ONNX format for cross-platform compatibility.

### 4. Start Development Environment

#### Option A: Docker Compose (Recommended)
```bash
# Start all services
docker compose up

# View logs
docker compose logs -f

# Stop services
docker compose down
```

**Service Endpoints:**
- **Application**: http://localhost:80
- **Backend API**: http://localhost:80/api
- **MinIO Console**: http://localhost:9001 (minioadmin:minioadmin)
- **Qdrant Dashboard**: http://localhost:6333/dashboard

#### Option B: Manual Development

**Backend Setup:**
```bash
cd backend/AzurePhotoFlow.Api

# Restore dependencies
dotnet restore

# Build project
dotnet build

# Run API (http://localhost:5000)
dotnet run
```

**Frontend Setup:**
```bash
cd frontend

# Install dependencies
npm install

# Start development server (http://localhost:5173)
npm run dev
```

## Development Workflow

### Backend Development

The backend uses clean architecture with dependency injection:

```
backend/AzurePhotoFlow.Api/
├── Controllers/          # API endpoints
├── Services/            # Business logic
├── Interfaces/          # Service abstractions
├── Models/             # Data transfer objects
└── Extensions/         # Service configuration
```

**Running Tests:**
```bash
cd tests/backend
dotnet test
```

**Database Migrations:**
The application uses code-first approach with automatic migrations.

### Frontend Development

The frontend is built with React 18 and modern tooling:

```
frontend/src/
├── components/         # Reusable UI components
├── pages/             # Page components
├── services/          # API clients
├── styles/            # CSS stylesheets
└── utils/             # Helper functions
```

**Running Tests:**
```bash
cd tests/frontend
npm test
```

**Building for Production:**
```bash
cd frontend
npm run build
```

### AI/ML Development

**Model Development:**
```bash
# Activate Python environment
source .venv/bin/activate

# Install additional packages for development
pip install jupyter notebook matplotlib

# Start Jupyter for model experimentation
jupyter notebook
```

**Testing Embeddings:**
```bash
# Test CLIP model inference
python -c "
from backend.Services.OnnxImageEmbeddingModel import OnnxImageEmbeddingModel
model = OnnxImageEmbeddingModel('/models/model.onnx')
print('Model loaded successfully')
"
```

## Configuration Details

### Google OAuth Setup

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select existing
3. Enable Google+ API
4. Create OAuth 2.0 credentials
5. Add authorized origins:
   - `http://localhost:80` (development)
   - `https://yourdomain.com` (production)
6. Copy Client ID to `VITE_GOOGLE_CLIENT_ID`

### MinIO Configuration

For development, MinIO runs with default credentials:
- **Endpoint**: `localhost:9000`
- **Console**: `localhost:9001`
- **Access Key**: `minioadmin`
- **Secret Key**: `minioadmin`

### Qdrant Configuration

Qdrant vector database settings:
- **URL**: `localhost:6333`
- **Collection**: `test` (created automatically)
- **Vector Size**: 512 (CLIP embedding dimension)
- **Distance**: Cosine similarity

## Troubleshooting

### Common Issues

**Port Conflicts:**
```bash
# Check port usage
lsof -i :80
lsof -i :5000
lsof -i :9000

# Stop conflicting services
docker compose down
```

**Model Loading Errors:**
```bash
# Verify model file exists
ls -la models/model.onnx

# Re-export model if corrupted
python scripts/ai-ml/export_clip_onnx.py --output models/model.onnx --force
```

**Database Connection Issues:**
```bash
# Check Qdrant connectivity
curl http://localhost:6333/health

# Check MinIO connectivity
curl http://localhost:9000/minio/health/live
```

**Build Errors:**
```bash
# Clean and rebuild backend
cd backend/AzurePhotoFlow.Api
dotnet clean
dotnet restore
dotnet build

# Clean and rebuild frontend
cd frontend
rm -rf node_modules package-lock.json
npm install
```

### Development Tips

**Hot Reload:**
- Backend: Automatic with `dotnet watch run`
- Frontend: Automatic with Vite dev server
- Docker: Use `docker compose watch` (Docker Compose v2.22+)

**Debugging:**
- Backend: Use Visual Studio Code with C# extension
- Frontend: Use browser DevTools and React DevTools
- API: Test endpoints with Swagger UI at `/swagger`

**Performance:**
- Use `npm run build` for production frontend builds
- Enable production optimizations in appsettings.json
- Monitor resource usage with `docker stats`

## Next Steps

Once your development environment is running:

1. **Test the application**: Upload some photos and try searching
2. **Explore the API**: Visit `/swagger` for interactive documentation
3. **Review the architecture**: Read [Architecture Guide](architecture.md)
4. **Deploy to production**: Follow [Deployment Guide](CICD_DEPLOYMENT.md)

## Getting Help

- **Documentation**: Check the `docs/` directory for detailed guides
- **Issues**: Report bugs on [GitHub Issues](https://github.com/Loicniragire/AzurePhotoFlow/issues)
- **API Reference**: Visit `/swagger` when running the backend
- **Logs**: Use `docker compose logs` to debug issues