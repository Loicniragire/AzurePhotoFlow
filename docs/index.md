# AzurePhotoFlow Documentation

Welcome to AzurePhotoFlow - a cloud-native AI-powered photo management application built with open-source technologies for cost-effective deployment.

## Quick Links

- [🏠 **Overview & Architecture**](architecture.md) - System design and components
- [🚀 **Getting Started**](../README.md) - Installation and setup guide
- [⚙️ **API Documentation**](api_endpoints.md) - REST API reference
- [🔗 **Frontend Integration**](frontend_integration.md) - Frontend API integration guide
- [🐳 **Deployment Guide**](CICD_DEPLOYMENT.md) - CI/CD and production deployment
- [☸️ **Kubernetes Setup**](CLUSTER_PREPARATION.md) - Container orchestration guide

## Features

- **🤖 AI-Powered Search**: Natural language queries using CLIP vision and text models
- **🖼️ Smart Photo Management**: Automated tagging and organization
- **👥 Face Recognition**: Person identification and tagging
- **📝 OCR Support**: Text extraction from images
- **💾 Cost-Effective Storage**: MinIO S3-compatible object storage
- **🔍 Vector Search**: Qdrant-powered similarity search

## Architecture Highlights

- **Frontend**: React 18 + Vite + Material-UI
- **Backend**: ASP.NET Core 8 with clean architecture
- **AI/ML**: CLIP vision and text models on ONNX Runtime for semantic search
- **Storage**: MinIO for objects, Qdrant for vectors
- **Deployment**: Kubernetes with MicroK8s
- **Authentication**: Google OAuth with JWT

## Quick Start

```bash
# Clone the repository
git clone https://github.com/Loicniragire/AzurePhotoFlow.git
cd AzurePhotoFlow

# Start all services with Docker Compose
docker compose up

# Access the application
open http://localhost:80
```

For detailed setup instructions, see the [Getting Started Guide](../README.md).

## Navigation

| Section | Description |
|---------|-------------|
| [Architecture](architecture.md) | System design and component interaction |
| [API Endpoints](api_endpoints.md) | REST API documentation |
| [Frontend Integration](frontend_integration.md) | Frontend API integration and authentication |
| [Setup Guide](setup.md) | Development environment setup |
| [Deployment](CICD_DEPLOYMENT.md) | Production deployment strategies |
| [UI Guidelines](ui_guidelines.md) | Frontend development standards |

---

Built with ❤️ using open-source technologies for cost-effective AI photo management.