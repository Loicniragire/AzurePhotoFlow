# Project Title: AzurePhotoFlow

## Overview
AzurePhotoFlow is a cloud-native application designed to help users manage, analyze, and search their photo collections. It leverages open-source services and self-hosted infrastructure for cost-effective, scalable photo processing and storage.

## Features
- **User Authentication (Google Login):** Secure user registration and login via Google.
- **Image Upload and Storage:** Securely upload and store photos in MinIO S3-compatible object storage.
- **AI-Based Photo Tagging and Classification:** Automated image analysis to generate descriptive tags and categories.
- **Face Recognition:** Identify and tag individuals in photos.
- **Optical Character Recognition (OCR):** Extract text from images.
- **Semantic Search (Natural Language Search):** Search photos using natural language queries.
- **Metadata-based Search:** Search photos based on metadata like filename, date, and tags.
- **Vector Embeddings:** Embeddings are computed during upload and stored directly in Qdrant for similarity search.

## Architecture
AzurePhotoFlow utilizes a modern cloud-native architecture with open-source services for cost efficiency:
- **Frontend:** React 18 + Vite + Material-UI single-page application
- **Backend:** ASP.NET Core 8 Web API with clean architecture patterns
- **Core Services:**
    - **MinIO:** S3-compatible object storage for photo uploads and file management
    - **Qdrant:** Vector database for embeddings storage and similarity search
    - **CLIP Model:** OpenAI CLIP vision transformer running on ONNX Runtime for semantic search
    - **Google OAuth:** User authentication and authorization
    - **Docker/Kubernetes:** Container orchestration and deployment
    - **Nginx:** Reverse proxy and load balancing

[A simple diagram or further details can be added here later.]

## Getting Started

### Prerequisites
- .NET SDK (8.0 or later)
- Node.js and npm (LTS versions, e.g., Node 18.x/20.x, npm 9.x/10.x or later)
- Docker and Docker Compose
- Python 3.8+ (for CLIP model export)
- Git
- Kubernetes (optional, for production deployment)
- Terraform (optional, for infrastructure management)

### Exporting the CLIP Model
The backend expects an ONNX version of the CLIP vision model. Create the Python virtual environment first:

```bash
python scripts/setup_venv.py --path .venv
source .venv/bin/activate  # on Windows use .venv\Scripts\activate
```

Then run the helper script to export the model:

```bash
python scripts/export_clip_onnx.py --output models/model.onnx
```

This downloads the pre-trained model and saves the ONNX file under `models/`. The `docker-compose.yml` mounts this directory so the backend container can access the model at `/models/model.onnx`.

### Backend Setup
```bash
# Navigate to the backend API directory
cd backend/AzurePhotoFlow.Api
# Restore dependencies
dotnet restore
# Build the project
dotnet build
# Run the application (typically starts on http://localhost:5000 or https://localhost:5001)
dotnet run
```

### Docker Compose Setup
Start all services including the backend API, frontend, MinIO object storage, and Qdrant vector database:

```bash
docker compose up
```

**Service Endpoints:**
- **Application:** `http://localhost:80` (via Nginx reverse proxy)
- **MinIO Storage:** `http://localhost:9000` (API) / `http://localhost:9001` (Console)
- **Qdrant Dashboard:** `http://localhost:6333/dashboard`
- **Default Credentials:** MinIO uses `minioadmin:minioadmin`

### Frontend Setup
```bash
# Navigate to the frontend directory
cd frontend
# Install dependencies
npm install
# Start the development server (typically opens in your browser)
npm run dev
```

### Infrastructure Setup
The infrastructure for AzurePhotoFlow is managed using Terraform. Scripts are located in the `infrastructure/` directory. Refer to the `docs/setup.md` for detailed deployment instructions (even if the doc is currently empty).

## Configuration
The backend limits upload sizes for multipart requests. By default the limit is **100MB**. To allow larger uploads (for example large zip archives), set the environment variable `MAX_UPLOAD_SIZE_MB` to the desired size in megabytes before starting the backend.

## Usage
[Placeholder - This section will be updated with detailed instructions on how to use the application's features. Key functionalities include user registration/login, photo uploading, browsing photo galleries, searching for photos using various criteria (tags, text in images, natural language), and viewing recognized faces.]

## API Documentation
For detailed information about the API endpoints, please refer to the [API Documentation](docs/api_endpoints.md).

## Contributing
Contributions are welcome! Please fork the repository and submit a pull request with your changes. For major changes, please open an issue first to discuss what you would like to change.

## License
This project is licensed under the MIT License.
