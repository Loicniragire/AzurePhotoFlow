# Project Title: AzurePhotoFlow

## Overview
AzurePhotoFlow is a cloud-native application designed to help users manage, analyze, and search their photo collections. It leverages various Azure services for robust and scalable photo processing and storage.

## Features
- **User Authentication (Google Login):** Secure user registration and login via Google.
- **Image Upload and Storage:** Securely upload and store photos in Azure Blob Storage.
- **AI-Based Photo Tagging and Classification:** Automated image analysis to generate descriptive tags and categories.
- **Face Recognition:** Identify and tag individuals in photos.
- **Optical Character Recognition (OCR):** Extract text from images.
- **Semantic Search (Natural Language Search):** Search photos using natural language queries.
- **Metadata-based Search:** Search photos based on metadata like filename, date, and tags.
- **Vector Embeddings:** Embeddings are computed during upload and stored directly in Qdrant for similarity search.

## Architecture
AzurePhotoFlow utilizes a modern cloud architecture with the following key components:
- **Frontend:** A React-based single-page application using Vite for building and development.
- **Backend:** An ASP.NET Core Web API that handles business logic, image processing, and interaction with Azure services.
- **Azure Services:**
    - **Azure Blob Storage:** For storing photo uploads.
    - **Azure Functions:** For serverless image processing tasks.
    - **Azure Cosmos DB or Azure SQL Database:** For storing metadata and user information.
    - **Azure Computer Vision:** For image analysis and tagging.
    - **Azure Cognitive Search:** For advanced search capabilities.
    - **Azure App Service:** For hosting the backend API.
    - **User Authentication:** Google Login is implemented for user authentication and authorization, managed via the backend API.
    - **Azure API Management:** To manage and secure APIs.

[A simple diagram or further details can be added here later.]

## Getting Started

### Prerequisites
- .NET SDK (6.0 or later)
- Node.js and npm (LTS versions, e.g., Node 18.x/20.x, npm 9.x/10.x or later)
- Azure CLI
- Terraform
- Git
- The environment variable `EMBEDDING_SERVICE_URL` should point to the HTTP endpoint of your embedding service (e.g. `http://embedding:80/api/Embedding`).
- The embedding service itself requires `QDRANT_URL`, `QDRANT_COLLECTION`, and `CLIP_MODEL_PATH` to be configured.
- `CLIP_MODEL_PATH` must reference a valid ONNX model file accessible to the backend container. If this file is missing, the backend will fail to start with an *Inference service is not configured* error.

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

## Usage
[Placeholder - This section will be updated with detailed instructions on how to use the application's features. Key functionalities include user registration/login, photo uploading, browsing photo galleries, searching for photos using various criteria (tags, text in images, natural language), and viewing recognized faces.]

## API Documentation
For detailed information about the API endpoints, please refer to the [API Documentation](docs/api_endpoints.md).

## Contributing
Contributions are welcome! Please fork the repository and submit a pull request with your changes. For major changes, please open an issue first to discuss what you would like to change.

## License
This project is licensed under the MIT License.
