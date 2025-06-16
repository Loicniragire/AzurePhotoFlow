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

# Export CLIP model to ONNX format (required for backend)
python scripts/ai-ml/export_clip_onnx.py --output models/model.onnx
```

### Docker Development
```bash
# Start all services (backend, frontend, MinIO, Qdrant)
docker compose up

# MinIO admin console: http://localhost:9001 (minioadmin:minioadmin)
# Qdrant dashboard: http://localhost:6333/dashboard
```

## Architecture Overview

AzurePhotoFlow is a cloud-native AI-powered photo management application with:

### Core Components
- **Frontend**: React 18 + Vite + Material-UI for responsive photo management interface
- **Backend**: ASP.NET Core 8 Web API with clean architecture patterns
- **AI/ML**: CLIP vision model for semantic search and image embeddings
- **Storage**: MinIO (local) / Azure Blob Storage (production) for images
- **Database**: Qdrant vector database for embeddings, Azure Cosmos DB for metadata
- **Authentication**: Google OAuth with JWT tokens

### Backend Projects Structure
- `AzurePhotoFlow.Api` - Main Web API application
- `AzurePhotoFlow.Functions` - Azure Functions for serverless processing
- `AzurePhotoFlow.POCO` - Data models and DTOs
- `AzurePhotoFlow.Shared` - Shared utilities across projects

### Key Features
- **Semantic Search**: Natural language queries using CLIP embeddings
- **Face Recognition**: Automated face detection and tagging
- **OCR**: Text extraction from images  
- **Metadata Extraction**: EXIF data processing
- **Bulk Upload**: ZIP file support with project organization

## Environment Configuration

### Required Environment Variables
- `VITE_GOOGLE_CLIENT_ID` - Google OAuth client ID
- `JWT_SECRET_KEY` - JWT token signing key
- `QDRANT_URL` - Vector database connection
- `QDRANT_COLLECTION` - Collection name for embeddings
- `CLIP_MODEL_PATH` - Path to ONNX model file (`/models/model.onnx`)
- `MINIO_ENDPOINT`, `MINIO_ACCESS_KEY`, `MINIO_SECRET_KEY` - Object storage config
- `ALLOWED_ORIGINS` - CORS origins (comma-separated)

### Development vs Production
- Use `MODE=development` for local development with MinIO
- Use `MODE=production` for Azure deployment with Blob Storage

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

### Azure Deployment
- Terraform manages infrastructure as code
- Azure DevOps handles CI/CD pipeline
- Container images pushed to GitHub Container Registry

The solution file `AzurePhotoFlow.generated.sln` contains all projects for comprehensive building and testing.