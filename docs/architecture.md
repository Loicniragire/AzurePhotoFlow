# AzurePhotoFlow Architecture

## System Overview

AzurePhotoFlow is a cloud-native AI-powered photo management application built with open-source technologies for cost-effective deployment. The system uses a microservices architecture with containerized components orchestrated by Kubernetes.

## Architecture Diagram

```
+-------------------+    +-------------------+    +-------------------+
|   Frontend        |    |   Backend API     |    |   AI/ML Engine    |
|   React + Vite    |--->|   ASP.NET Core    |--->|   CLIP Vision +   |
|   Material-UI     |    |   Clean Arch      |    |   Text + ONNX     |
+-------------------+    +-------------------+    +-------------------+
         |                       |                       |
         |                       v                       |
         |              +-------------------+            |
         |              |   MinIO Storage   |            |
         |              |   S3-Compatible   |            |
         |              +-------------------+            |
         |                                                |
         +------------------------+-------------------------+
                                  v
                    +-------------------+
                    |   Qdrant Vector   |
                    |   Similarity DB   |
                    +-------------------+
```

## Core Components

### 1. Frontend Application
**Technology**: React 18 + Vite + Material-UI v6

**Responsibilities**:
- Responsive user interface for photo management
- Google OAuth authentication flow
- Photo upload with ZIP file support
- Natural language search interface
- Face recognition tagging interface
- Real-time search results display

**Key Features**:
- Client-side routing with React Router v7
- State management with React Query v3
- HTTP client with Axios
- JWT token management
- Progressive Web App capabilities

### 2. Backend API
**Technology**: ASP.NET Core 8 Web API

**Architecture Pattern**: Clean Architecture with dependency injection

**Controllers**:
- `ImageController`: Photo uploads, project management, metadata extraction
- `SearchController`: Vector similarity search and natural language queries
- `AuthController`: Google OAuth integration and JWT token management
- `EmbeddingController`: AI model inference and vector generation
- `OCRController`: Text extraction from images
- `FaceRecognitionController`: Face detection and person tagging

**Services**:
- `EmbeddingService`: CLIP vision and text model interface for vector generation
- `ImageUploadService`: File processing and storage management
- `SearchService`: Query processing and result ranking
- `VectorStore`: Qdrant database abstraction
- `MetadataExtractorService`: EXIF data processing

### 3. AI/ML Engine
**Technology**: OpenAI CLIP vision and text transformers on ONNX Runtime

**Model Details**:
- Pre-trained CLIP vision model (`openai/clip-vit-base-patch32`) - 350MB ONNX
- Pre-trained CLIP text model (`openai/clip-vit-base-patch32`) - 252MB ONNX
- 38,400-dimensional vector embeddings (50 x 768)
- Cross-platform inference with ONNX Runtime
- Local processing for cost efficiency

**Capabilities**:
- Semantic image understanding using vision encoder
- Natural language query processing using text encoder
- Accurate text-to-image similarity scoring in shared embedding space
- Face detection and recognition
- OCR text extraction

### 4. Storage Systems

#### Object Storage (MinIO)
**Technology**: MinIO S3-compatible object storage

**Organization**:
```
Bucket Structure:
├── {year}/
│   └── {timestamp}/
│       └── {projectName}/
│           └── {directoryName}/
│               └── {fileName}
```

**Features**:
- S3-compatible API
- Hierarchical organization
- Direct URL access with signed URLs
- EXIF metadata preservation
- Multi-format support (JPEG, PNG, TIFF, etc.)

#### Vector Database (Qdrant)
**Technology**: Qdrant vector similarity search engine

**Collections**:
- Environment-specific collections
- 512-dimensional CLIP embeddings
- Metadata storage with vectors
- Cosine similarity search

**Data Model**:
```json
{
  "id": "md5_hash_of_object_key",
  "vector": [512 float values],
  "payload": {
    "object_key": "storage/path/to/image.jpg",
    "file_name": "image.jpg",
    "project_name": "vacation_photos",
    "upload_timestamp": "2024-01-01T12:00:00Z"
  }
}
```

### 5. Authentication & Security
**Technology**: Google OAuth 2.0 + JWT tokens

**Flow**:
1. User initiates Google OAuth login
2. Backend validates OAuth token with Google
3. Backend generates JWT with user claims
4. Frontend stores JWT for API authentication
5. API validates JWT on protected endpoints

**Security Features**:
- HTTPS-only communication
- JWT token expiration
- Role-based access control
- CORS configuration
- Input validation and sanitization

## Data Flow Patterns

### Image Upload Flow
```
1. User uploads ZIP file -> Frontend
2. Frontend sends to -> Backend API
3. Backend extracts images -> MinIO Storage
4. Background processing:
   a. Extract EXIF metadata
   b. Generate CLIP embeddings
   c. Store vectors in Qdrant
   d. Index for search
```

### Search Flow
```
1. User enters query -> Frontend
2. Frontend sends to -> Backend Search API
3. Backend processes:
   a. Generate query embedding (CLIP)
   b. Perform vector similarity search (Qdrant)
   c. Rank results by similarity score
   d. Fetch metadata and URLs
4. Results returned -> Frontend display
```

### Face Recognition Flow
```
1. Image upload triggers -> Face detection service
2. Extract face embeddings -> Store in Qdrant
3. Group similar faces -> Create person clusters
4. User labels persons -> Update metadata
5. Search by person -> Return tagged photos
```

## Deployment Architecture

### Development Environment
```
Docker Compose:
├── nginx (Reverse Proxy)
├── frontend (React App)
├── backend (API Server)
├── minio (Object Storage)
└── qdrant (Vector Database)
```

### Production Environment
```
Kubernetes Cluster:
├── Namespace: azurephotoflow
├── Ingress: NGINX with SSL
├── Deployments:
│   ├── frontend-deployment (3 replicas)
│   ├── backend-deployment (3 replicas)
│   ├── minio-deployment (1 replica)
│   └── qdrant-deployment (1 replica)
├── Services: Load balancing
├── PersistentVolumes: Data persistence
└── ConfigMaps/Secrets: Configuration
```

### Container Images
- **Frontend**: Multi-stage Dockerfile with Nginx
- **Backend**: .NET 8 runtime with ONNX model
- **Storage**: Official MinIO and Qdrant images

## Performance Characteristics

### Scalability
- **Horizontal scaling**: Frontend and backend can scale independently
- **Load balancing**: NGINX distributes requests across replicas
- **Caching**: React Query caches API responses
- **Database**: Qdrant handles large vector collections efficiently

### Performance Metrics
- **Search latency**: <500ms for similarity search
- **Upload throughput**: Depends on network and storage
- **Concurrent users**: Scales with replica count
- **Storage efficiency**: Vector compression in Qdrant

## Technology Stack Summary

| Component | Technology | Version | Purpose |
|-----------|------------|---------|---------|
| Frontend | React | 18.x | User interface |
| Build Tool | Vite | 5.x | Development and bundling |
| UI Framework | Material-UI | 6.x | Component library |
| Backend | ASP.NET Core | 8.x | API server |
| AI Model | CLIP | ONNX | Semantic embeddings |
| Object Storage | MinIO | Latest | File storage |
| Vector DB | Qdrant | Latest | Similarity search |
| Orchestration | Kubernetes | 1.20+ | Container management |
| Reverse Proxy | NGINX | Latest | Load balancing |
| Authentication | Google OAuth | 2.0 | User authentication |

## Key Design Decisions

### Cost Optimization
- **Open-source stack**: Eliminates cloud service costs
- **Self-hosted infrastructure**: Full control over resources
- **Local AI processing**: No external API charges
- **Efficient storage**: S3-compatible with local deployment

### Scalability Strategy
- **Microservices architecture**: Independent scaling
- **Stateless design**: Easy horizontal scaling
- **Container-first**: Cloud-native deployment
- **Event-driven processing**: Async background tasks

### Performance Focus
- **Vector search**: Optimized similarity algorithms
- **Caching layers**: Multi-level caching strategy
- **Efficient protocols**: HTTP/2, compression
- **Resource optimization**: Memory and CPU tuning

### Security Considerations
- **Zero-trust model**: Authentication on all endpoints
- **Encrypted communication**: HTTPS everywhere
- **Input validation**: Comprehensive sanitization
- **Access control**: Role-based permissions

This architecture provides a robust, scalable, and cost-effective solution for AI-powered photo management while maintaining high performance and security standards.