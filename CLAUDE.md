# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Frontend (React + Vite)
```bash
cd frontend
npm install          # Install dependencies
npm run dev          # Start development server
npm run build        # Production build
npm run lint         # ESLint code quality check
npm run preview      # Preview production build

# Frontend testing
cd tests/frontend
npm install          # Install test dependencies
npm run test         # Run Vitest tests
npm run test:ui      # Run Vitest with UI
```

### Backend (.NET 8)
```bash
cd backend/AzurePhotoFlow.Api
dotnet restore       # Restore NuGet packages
dotnet build         # Build solution
dotnet run           # Run API (http://localhost:5000)
dotnet test          # Run tests

# For running tests across all projects
cd tests/backend
dotnet test backend.tests.sln
```

### AI/ML Model Setup
```bash
# Setup Python virtual environment for CLIP model
python scripts/ai-ml/setup_venv.py --path .venv
source .venv/bin/activate  # Windows: .venv\Scripts\activate

# AUTOMATED MODEL EXPORT (RECOMMENDED)
# Auto-export models based on .env configuration
python scripts/ai-ml/auto_export_models.py

# Complete automated setup (includes model export)
make setup
# or
./scripts/setup/auto-setup.sh

# MANUAL MODEL EXPORT (if needed)
# Base model (512 dimensions) - default
python scripts/ai-ml/export_clip_onnx.py --variant base --output models

# Large model (768 dimensions) - better accuracy, more resource intensive
python scripts/ai-ml/export_clip_onnx.py --variant large --output models

# Huge model (1024 dimensions) - best accuracy, most resource intensive
python scripts/ai-ml/export_clip_onnx.py --variant huge --output models

# QUICK MODEL SWITCHING
make models-512    # Switch to base model (512D)
make models-768    # Switch to large model (768D)
make models-1024   # Switch to huge model (1024D)

# This creates:
# - models/vision_model.onnx (for image embeddings)
# - models/text_model.onnx (for text embeddings)  
# - models/tokenizer/ (CLIP tokenizer files)
# - models/model.onnx (backward compatibility symlink)
# - models/model_info.txt (tracks current model variant)
```

### Docker Development
```bash
# AUTOMATED SETUP (RECOMMENDED)
make setup              # Complete setup with model auto-export
make dev               # Start with model verification
make deploy-768        # Quick deploy with large model (768D)

# MANUAL DOCKER COMMANDS
docker compose up      # Start all services (backend, frontend, MinIO, Qdrant)

# Service URLs:
# - MinIO admin console: http://localhost:9001 (minioadmin:minioadmin)
# - Qdrant dashboard: http://localhost:6333/dashboard
# - Backend API: http://localhost:5001
# - Frontend: http://localhost:8080
```

### Development Workflow
```bash
# 1. One-time setup
make setup

# 2. Change embedding dimension (edit .env or use shortcuts)
make models-768        # Switch to 768-dimensional embeddings

# 3. Start development
make dev              # Models are auto-verified and exported if needed

# 4. Check current configuration
make config           # Show current .env settings
make check-models     # Verify models match configuration
```

### API Testing and Swagger Authentication
```bash
# Generate JWT token for API testing
bash scripts/generate-swagger-token.sh

# The backend API is accessible at:
# - http://localhost:5001 (direct backend access)
# - OpenAPI spec: http://localhost:5001/swagger/v1/swagger.json

# For API testing, use the generated JWT token in Authorization header:
# Authorization: Bearer <token>

# Example API calls:
TOKEN="<generated-token>"
curl -H "Authorization: Bearer $TOKEN" http://localhost:5001/api/search/get-count
curl -H "Authorization: Bearer $TOKEN" http://localhost:5001/api/auth/check
```

**Note**: The Swagger UI interface may not be accessible directly. For full API testing capabilities:
1. Import the OpenAPI spec (`http://localhost:5001/swagger/v1/swagger.json`) into Postman
2. Use the online Swagger Editor at https://editor.swagger.io/
3. Use curl commands with the generated JWT token

## Architecture Overview

AzurePhotoFlow is a cloud-native AI-powered photo management application with:

### Core Components
- **Frontend**: React 18 + Vite + Material-UI for responsive photo management interface
- **Backend**: ASP.NET Core 8 Web API with clean architecture patterns
- **AI/ML**: CLIP vision and text models for semantic search and image embeddings
- **Storage**: MinIO S3-compatible object storage for images and file management
- **Database**: Qdrant vector database for embeddings and similarity search
- **Authentication**: Google OAuth with JWT tokens

### Backend Projects Structure
- `AzurePhotoFlow.Api` - Main Web API application with integrated AI processing
- `AzurePhotoFlow.Functions` - Background processing functions (legacy Azure Functions structure)
- `AzurePhotoFlow.POCO` - Data models and DTOs
- `AzurePhotoFlow.Shared` - Shared utilities across projects

### Key Features
- **Semantic Search**: Natural language queries using CLIP text and vision embeddings
- **Face Recognition**: Automated face detection and tagging
- **OCR**: Text extraction from images  
- **Metadata Extraction**: EXIF data processing
- **Bulk Upload**: ZIP file support with project organization
- **Advanced AI**: Real CLIP text encoder for accurate text-to-image semantic matching

## Environment Configuration

### Required Environment Variables
- `VITE_GOOGLE_CLIENT_ID` - Google OAuth client ID
- `JWT_SECRET_KEY` - JWT token signing key
- `QDRANT_URL` - Qdrant vector database connection (e.g., `localhost:6333`)
- `QDRANT_COLLECTION` - Collection name for embeddings storage
- `CLIP_MODEL_PATH` - Path to ONNX vision model file (`/models/model.onnx` or `/models/vision_model.onnx`)
- `MAX_UPLOAD_SIZE_MB` - Maximum file upload size in megabytes (default: 100MB)
- `MINIO_ENDPOINT` - MinIO server endpoint (e.g., `localhost:9000`)
- `MINIO_ACCESS_KEY` - MinIO access key (default: `minioadmin`)
- `MINIO_SECRET_KEY` - MinIO secret key (default: `minioadmin`)
- `ENABLE_EMBEDDINGS` - Enable/disable AI embeddings processing (`true`/`false`)
- `ALLOWED_ORIGINS` - CORS origins (comma-separated)
- `OPENAI_API_KEY` - OpenAI API key for additional AI features (optional)

### Embedding Configuration (Optional Environment Overrides)
- `EMBEDDING_DIMENSION` - Override embedding dimension (512, 768, 1024)
- `EMBEDDING_MODEL_VARIANT` - Override model variant (base, large, huge)  
- `EMBEDDING_DISTANCE_METRIC` - Override distance metric (Cosine, Dot, Euclidean)

**Configuration in appsettings.json:**
```json
{
  "EmbeddingConfig": {
    "EmbeddingDimension": 512,      // 512 (base), 768 (large), 1024 (huge)
    "ModelVariant": "base",         // "base", "large", "huge"
    "DistanceMetric": "Cosine",     // "Cosine", "Dot", "Euclidean"
    "EnableTextPreprocessing": true, // Improves search accuracy
    "MaxTokenLength": 77,           // CLIP standard token limit
    "ImageInputSize": 224           // CLIP standard image size
  }
}
```

**Note**: When changing embedding dimensions, ensure:
1. Export the correct CLIP model variant using the export script
2. Update the configuration to match the model's output dimensions  
3. Recreate Qdrant collections (existing data will be incompatible)

### Development vs Production
- Use `MODE=development` for local development
- Use `MODE=production` for production deployment
- Both modes use MinIO for object storage and Qdrant for vector database

## Testing

### Backend Tests
```bash
# Run all backend tests
cd tests/backend
dotnet test

# Individual test projects
dotnet test AzurePhotoFlow.Api.Tests/
dotnet test AzurePhotoFlow.Functions.Tests/
```

### Test Structure
- Unit tests for API controllers and services
- Integration tests for MinIO/Qdrant connections
- Test utilities in `AzurePhotoFlow.Tests.Utilities`
- Frontend tests using Vitest and React Testing Library in `tests/frontend/`

## Key Patterns & Conventions

### Backend Architecture
- Clean Architecture with dependency injection
- Repository pattern for data access
- Service layer for business logic
- Controller-based REST APIs

### Code Organization
- Use existing project structure and naming conventions
- Follow .NET coding standards and practices
- Implement proper error handling and logging
- Use async/await patterns for I/O operations

### AI/ML Integration
- CLIP model runs in ONNX Runtime for cross-platform compatibility
- Vector embeddings stored directly in Qdrant during upload
- Face recognition and OCR services integrated into upload pipeline

### Scripts Organization
The `scripts/` directory is organized into logical categories:

- **deployment/**: CI/CD and production deployment scripts
  - `smart-deploy.sh` - Intelligent deployment orchestration
  - `check-cluster-config.py` - Cluster configuration analysis
- **setup/**: Initial installation and configuration
  - `setup-microk8s.sh` - Complete MicroK8s cluster setup
  - `prepare-microk8s.sh` - Cluster validation and preparation
  - `setup-secrets.sh` - Kubernetes secrets management
  - `install-components.sh` - Optional component installation
- **ai-ml/**: AI/ML model processing and setup
  - `export_clip_onnx.py` - CLIP model export to ONNX format
  - `setup_venv.py` - Python virtual environment setup
- **monitoring/**: Operational monitoring and troubleshooting
  - `monitor-k8s.sh` - Kubernetes cluster monitoring dashboard
- **debugging/**: Development and debugging utilities
  - `debug-deployment.sh` - Deployment troubleshooting toolkit

## Infrastructure

### Local Development
- Docker Compose orchestrates all services
- MinIO provides S3-compatible object storage
- Qdrant handles vector similarity search

### Production Deployment
- Kubernetes with MicroK8s for container orchestration
- Terraform manages infrastructure as code
- GitHub Container Registry for container images
- Self-hosted infrastructure to reduce cloud costs

The solution file `AzurePhotoFlow.generated.sln` contains all projects for comprehensive building and testing.