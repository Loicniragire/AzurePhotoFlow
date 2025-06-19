# AzurePhotoFlow API Documentation

## Overview

The AzurePhotoFlow API is a .NET 8 Web API application designed for AI-powered photo management with semantic search capabilities. It provides endpoints for user authentication, image upload/management, AI embedding generation, and project organization.

**Base URL**: 
- Development: `http://localhost:5000/api`
- Production: `https://your-domain.com/api`

**Authentication**: JWT Bearer tokens via Google OAuth

## Authentication

### Google OAuth Login

#### `POST /api/auth/google-login`

Authenticate users via Google OAuth and obtain a JWT token.

**Request Body:**
```json
{
  "Token": "string" // Google ID Token (required)
}
```

**Success Response (200):**
```json
{
  "message": "Login successful",
  "token": "string", // JWT token for subsequent requests
  "googleId": "string",
  "email": "string",
  "name": "string",
  "picture": "string"
}
```

**Error Response (400):**
```json
{
  "message": "Invalid Google token",
  "error": "string"
}
```

### Check Authentication Status

#### `GET /api/auth/check`

Check if the current JWT token is valid.

**Headers:**
```
Authorization: Bearer <jwt_token>
```

**Success Response (200):**
```json
{
  "isAuthenticated": true,
  "user": "string" // User identity name
}
```

### Logout

#### `POST /api/auth/logout`

Logout user (client-side token disposal).

**Success Response (200):**
```json
{
  "message": "Logout successful. Please discard the token on the client."
}
```

## Image Management

### Upload Raw Images

#### `POST /api/image/raw`

Upload raw image files as a ZIP archive.

**Authorization:** JWT Bearer token required (FullAccess role)

**Content-Type:** `multipart/form-data`

**Parameters:**
- `timeStamp` (DateTime, required): Timestamp for the upload
- `projectName` (string, required): Name of the project
- `directoryFile` (IFormFile, required): ZIP file containing images

**File Organization:**
Files are stored with the path structure: `{timestamp}/{projectName}/{directoryName}/{fileName}`

**Success Response (200):**
```json
{
  "Message": "Directory uploaded and files extracted successfully.",
  "Files": {
    "UploadedCount": 10,
    "OriginalCount": 12
  }
}
```

**Error Responses:**
- `400`: Missing required parameters
- `500`: Internal server error

### Upload Processed Images

#### `POST /api/image/processed`

Upload processed image files as a ZIP archive, associated with existing raw files.

**Authorization:** JWT Bearer token required

**Content-Type:** `multipart/form-data`

**Parameters:**
- `timeStamp` (DateTime, required): Timestamp for the upload
- `projectName` (string, required): Name of the project
- `rawfileDirectoryName` (string, required): Associated raw files directory name
- `directoryFile` (IFormFile, required): ZIP file containing processed images

**File Organization:**
Files are stored with the path: `{timestamp.year}/{timestamp}/{projectName}/{directoryName}/{fileName}`

**Success Response (200):**
```json
{
  "Message": "Processed directory uploaded and files extracted successfully.",
  "Files": {
    "UploadedCount": 8,
    "OriginalCount": 8
  }
}
```

**Error Responses:**
- `400`: Missing parameters or associated raw files not found
- `500`: Internal server error

### Delete Project

#### `DELETE /api/image/projects`

Delete a project and all associated files (raw and processed).

**Authorization:** JWT Bearer token required

**Query Parameters:**
- `projectName` (string, required): Name of the project to delete
- `timestamp` (DateTime, required): Timestamp of the project

**Success Response (200):**
```
"Project 'vacation_photos' deleted successfully."
```

**Error Responses:**
- `400`: Missing project name
- `500`: Internal server error

### Get Projects

#### `GET /api/image/projects`

Retrieve project information with optional filtering.

**Authorization:** JWT Bearer token required

**Query Parameters:**
- `year` (string, optional): Filter by year (e.g., "2024")
- `projectName` (string, optional): Filter by project name
- `timestamp` (string, optional): Filter by timestamp (format: 'yyyy-MM-dd')

**Success Response (200):**
```json
[
  {
    "ProjectName": "vacation_photos",
    "TimeStamp": "2024-01-15T10:30:00.000Z",
    "Directories": [
      {
        "DirectoryName": "beach_shots",
        "RawFilesCount": 25,
        "ProcessedFilesCount": 20
      },
      {
        "DirectoryName": "sunset_photos",
        "RawFilesCount": 15,
        "ProcessedFilesCount": 15
      }
    ]
  }
]
```

**Error Responses:**
- `400`: Invalid timestamp format
- `500`: Internal server error

## AI/ML Features

### Generate Embeddings

#### `POST /api/embedding/generate`

Generate CLIP model embeddings for images in a ZIP file for semantic search.

**Authorization:** JWT Bearer token required

**Content-Type:** `multipart/form-data`

**Request Parameters:**
- `ProjectName` (string, required): Name of the project
- `DirectoryName` (string, required): Name of the directory
- `Timestamp` (DateTime, required): Timestamp for the upload
- `ZipFile` (IFormFile, required): ZIP file containing images
- `IsRawFiles` (bool, optional, default: true): Whether files are raw or processed
- `RawDirectoryName` (string, optional): Raw directory name (required if IsRawFiles=false)

**Process:**
1. Extracts images from ZIP file
2. Generates 512-dimensional embeddings using CLIP model
3. Stores vectors in Qdrant database for similarity search
4. Associates metadata with vectors

**Success Response (200):** Empty OK response

**Error Response (400):**
```json
{
  "message": "Zip file must be provided"
}
```

### Check Embedding Service Status

#### `GET /api/embedding/status`

Check if the embedding service is running and available.

**Authorization:** JWT Bearer token required

**Success Response (200):**
```json
{
  "status": "Embedding service is running"
}
```

## Search & Discovery

### Semantic Search

#### `GET /api/search/semantic`

Search for images using natural language queries powered by AI embeddings.

**Authorization:** JWT Bearer token required

**Query Parameters:**
- `query` (string, required): Natural language search query (e.g., "photos of dogs in parks")
- `limit` (int, optional, default: 20): Maximum results to return (1-100)
- `threshold` (double, optional, default: 0.5): Minimum similarity threshold (0.0-1.0)
- `projectName` (string, optional): Filter results by specific project
- `year` (string, optional): Filter results by year

**Example Request:**
```bash
GET /api/search/semantic?query=dogs%20playing%20in%20water&limit=10&threshold=0.6
```

**Success Response (200):**
```json
{
  "Query": "dogs playing in water",
  "Results": [
    {
      "ObjectKey": "2024/1705401600/vacation_photos/beach_day/IMG_1234.jpg",
      "SimilarityScore": 0.87,
      "FileName": "IMG_1234.jpg",
      "ProjectName": "vacation_photos",
      "DirectoryName": "beach_day",
      "Year": "2024",
      "UploadDate": "2024-01-16T10:30:00Z",
      "ImageUrl": "https://storage.example.com/2024/...",
      "Metadata": {
        "path": "2024/1705401600/vacation_photos/beach_day/IMG_1234.jpg",
        "project_name": "vacation_photos"
      }
    }
  ],
  "TotalResults": 5,
  "ProcessingTimeMs": 245,
  "Success": true,
  "ErrorMessage": null
}
```

**Error Response (400):**
```json
{
  "Query": "",
  "Results": [],
  "TotalResults": 0,
  "ProcessingTimeMs": 12,
  "Success": false,
  "ErrorMessage": "Search query cannot be empty"
}
```

#### `POST /api/search/semantic`

Advanced semantic search with detailed request model for complex queries.

**Authorization:** JWT Bearer token required

**Request Body:**
```json
{
  "Query": "sunset photos with mountains",
  "Limit": 15,
  "Threshold": 0.7,
  "ProjectName": "landscape_photography",
  "Year": "2023"
}
```

**Response:** Same format as GET endpoint

### Search Features

**Natural Language Processing:**
- Understands context and semantic meaning
- Handles synonyms and related terms
- Works with descriptive phrases and scenes

**Filtering Options:**
- Project-based filtering for organized searches
- Year-based temporal filtering
- Configurable similarity thresholds for precision control

**Performance:**
- Sub-second response times for most queries
- Efficient vector similarity search
- Similarity scores for result ranking

### Visual Similarity Search

#### `GET /api/search/similarity`

Find visually similar images based on a reference image using CLIP embeddings.

**Authorization:** JWT Bearer token required

**Query Parameters:**
- `objectKey` (string, required): Object key/path of the reference image
- `limit` (int, optional, default: 20): Maximum results to return (1-100)
- `threshold` (double, optional, default: 0.5): Minimum similarity threshold (0.0-1.0)
- `projectName` (string, optional): Filter results by specific project
- `year` (string, optional): Filter results by year

**Example Request:**
```bash
GET /api/search/similarity?objectKey=2024/1705401600/vacation_photos/beach_day/IMG_1234.jpg&limit=10&threshold=0.7
```

**Success Response (200):**
```json
{
  "ReferenceObjectKey": "2024/1705401600/vacation_photos/beach_day/IMG_1234.jpg",
  "Results": [
    {
      "ObjectKey": "2024/1705401600/vacation_photos/beach_day/IMG_1235.jpg",
      "SimilarityScore": 0.89,
      "FileName": "IMG_1235.jpg",
      "ProjectName": "vacation_photos",
      "DirectoryName": "beach_day",
      "Year": "2024",
      "UploadDate": "2024-01-16T10:35:00Z",
      "ImageUrl": "https://storage.example.com/2024/...",
      "Metadata": {
        "path": "2024/1705401600/vacation_photos/beach_day/IMG_1235.jpg",
        "project_name": "vacation_photos"
      }
    }
  ],
  "TotalResults": 1,
  "ProcessingTimeMs": 156,
  "Success": true,
  "ErrorMessage": null
}
```

**Error Response (400):**
```json
{
  "ReferenceObjectKey": "",
  "Results": [],
  "TotalResults": 0,
  "ProcessingTimeMs": 8,
  "Success": false,
  "ErrorMessage": "Reference image object key cannot be empty"
}
```

**Error Response (404):**
```json
{
  "ReferenceObjectKey": "invalid/path.jpg",
  "Results": [],
  "TotalResults": 0,
  "ProcessingTimeMs": 45,
  "Success": false,
  "ErrorMessage": "Reference image not found or embedding not available"
}
```

#### `POST /api/search/similarity`

Advanced visual similarity search with detailed request model for complex queries.

**Authorization:** JWT Bearer token required

**Request Body:**
```json
{
  "ObjectKey": "2024/1705401600/vacation_photos/beach_day/IMG_1234.jpg",
  "Limit": 15,
  "Threshold": 0.8,
  "ProjectName": "vacation_photos",
  "Year": "2024"
}
```

**Response:** Same format as GET endpoint

### Similarity Search Features

**Visual Analysis:**
- Uses CLIP vision embeddings for semantic visual understanding
- Finds images with similar composition, colors, and subjects
- Works across different image formats and resolutions

**Smart Filtering:**
- Automatically excludes the reference image from results
- Project-based filtering for organized searches
- Year-based temporal filtering
- Configurable similarity thresholds for precision control

**Performance:**
- Leverages Qdrant vector database for fast similarity search
- Sub-second response times for most queries
- Cosine similarity scoring for accurate ranking

### Complex Multi-Criteria Search

#### `POST /api/search/query`

Perform advanced search queries combining semantic search, visual similarity, and metadata filters with flexible combination modes.

**Authorization:** JWT Bearer token required

**Request Body:**
```json
{
  "SemanticQuery": "dogs playing in water",
  "SimilarityReferenceKey": "2024/1705401600/vacation_photos/beach_day/IMG_1234.jpg",
  "Limit": 20,
  "Threshold": 0.6,
  "CombinationMode": "WeightedCombination",
  "SemanticWeight": 0.7,
  "SimilarityWeight": 0.3,
  "Filters": {
    "ProjectNames": ["vacation_photos", "family_trips"],
    "Years": ["2024"],
    "DirectoryNames": ["beach_day", "pool_party"],
    "FileExtensions": ["jpg", "png"],
    "UploadDateRange": {
      "StartDate": "2024-01-01T00:00:00Z",
      "EndDate": "2024-12-31T23:59:59Z"
    },
    "CustomFilters": {
      "camera_make": "Canon"
    }
  }
}
```

**Request Parameters:**
- `SemanticQuery` (string, optional): Natural language search query
- `SimilarityReferenceKey` (string, optional): Reference image for visual similarity
- `Limit` (int, 1-100, default: 20): Maximum results to return
- `Threshold` (double, 0.0-1.0, default: 0.5): Minimum similarity threshold
- `CombinationMode` (enum): How to combine multiple search types
  - `Union` (0): Combine all results (more results)
  - `Intersection` (1): Only results found in both searches (fewer, more relevant)
  - `WeightedCombination` (2): Weighted scoring based on search weights
- `SemanticWeight` (double, 0.0-1.0, default: 0.5): Weight for semantic search results
- `SimilarityWeight` (double, 0.0-1.0, default: 0.5): Weight for similarity search results
- `Filters` (object, optional): Advanced filtering options

**Search Filters:**
- `ProjectNames` (array): Filter by specific project names
- `Years` (array): Filter by specific years
- `DirectoryNames` (array): Filter by directory names
- `FileExtensions` (array): Filter by file extensions (e.g., "jpg", "png")
- `UploadDateRange` (object): Filter by upload date range
- `CustomFilters` (object): Custom key-value metadata filters

**Success Response (200):**
```json
{
  "Request": {
    "SemanticQuery": "dogs playing in water",
    "SimilarityReferenceKey": "2024/1705401600/vacation_photos/beach_day/IMG_1234.jpg",
    "CombinationMode": "WeightedCombination",
    "SemanticWeight": 0.7,
    "SimilarityWeight": 0.3
  },
  "Results": [
    {
      "ObjectKey": "2024/1705401600/vacation_photos/beach_day/IMG_1235.jpg",
      "RelevanceScore": 0.91,
      "SemanticScore": 0.88,
      "SimilarityScore": 0.95,
      "MatchedSearchTypes": ["semantic", "similarity"],
      "FileName": "IMG_1235.jpg",
      "ProjectName": "vacation_photos",
      "DirectoryName": "beach_day",
      "Year": "2024",
      "UploadDate": "2024-01-16T10:35:00Z",
      "ImageUrl": "https://storage.example.com/2024/...",
      "Metadata": {
        "path": "2024/1705401600/vacation_photos/beach_day/IMG_1235.jpg",
        "project_name": "vacation_photos"
      }
    }
  ],
  "TotalResults": 1,
  "ProcessingTimeMs": 287,
  "Breakdown": {
    "SemanticResults": 12,
    "SimilarityResults": 8,
    "OverlapResults": 3,
    "SemanticSearchTimeMs": 156,
    "SimilaritySearchTimeMs": 89,
    "CombinationTimeMs": 42
  },
  "Success": true,
  "ErrorMessage": null
}
```

**Error Response (400):**
```json
{
  "Request": null,
  "Results": [],
  "TotalResults": 0,
  "ProcessingTimeMs": 5,
  "Success": false,
  "ErrorMessage": "At least one search criteria must be provided (SemanticQuery or SimilarityReferenceKey)"
}
```

### Complex Search Features

**Multi-Modal Search:**
- Combines semantic text search with visual similarity search
- Flexible weighting system for different search types
- Multiple combination modes for different use cases

**Advanced Filtering:**
- Project and directory-based organization
- File type and date range filtering
- Custom metadata filtering capabilities
- Multiple filter values with OR/AND logic

**Intelligent Scoring:**
- Weighted combination of semantic and similarity scores
- Boost scoring for results found in multiple search types
- Normalized relevance scoring (0.0-1.0)

**Performance Analytics:**
- Detailed timing breakdown for each search component
- Result overlap analysis between search types
- Processing time tracking for optimization

**Combination Modes:**
- **Union**: Maximum recall - returns all results from both searches
- **Intersection**: Maximum precision - only results found in both searches
- **WeightedCombination**: Balanced approach with configurable weights

## Future Search Endpoints (Planned)

### Face Recognition (Coming Soon)
- `POST /api/facerecognition/detect` - Detect faces in images
- `GET /api/facerecognition/persons` - List recognized persons
- `POST /api/facerecognition/tag` - Tag persons in photos

### OCR (Coming Soon)
- `POST /api/ocr/extract` - Extract text from images
- `GET /api/ocr/search` - Search by extracted text

## Data Models

### ProjectInfo
```json
{
  "ProjectName": "string",
  "TimeStamp": "2024-01-01T00:00:00.000Z",
  "Directories": [
    {
      "DirectoryName": "string",
      "RawFilesCount": 0,
      "ProcessedFilesCount": 0
    }
  ]
}
```

### GoogleLoginRequest
```json
{
  "Token": "string" // Google ID Token
}
```

### UploadResponse
```json
{
  "UploadedCount": 0,
  "OriginalCount": 0
}
```

### ImageEmbedding
```json
{
  "ObjectKey": "string", // Storage path
  "Vector": [0.1, 0.2, 0.3, ...] // 512-dimensional float array
}
```

### SimilaritySearchRequest
```json
{
  "ObjectKey": "string", // Reference image path (required)
  "Limit": 20, // Max results (1-100, default: 20)
  "Threshold": 0.5, // Similarity threshold (0.0-1.0, default: 0.5)
  "ProjectName": "string", // Optional project filter
  "Year": "string" // Optional year filter
}
```

### SimilaritySearchResponse
```json
{
  "ReferenceObjectKey": "string", // Reference image path
  "Results": [
    {
      "ObjectKey": "string", // Similar image path
      "SimilarityScore": 0.85, // Similarity score (0.0-1.0)
      "FileName": "string",
      "ProjectName": "string",
      "DirectoryName": "string",
      "Year": "string",
      "UploadDate": "2024-01-01T00:00:00Z",
      "ImageUrl": "string",
      "Metadata": {
        "path": "string",
        "project_name": "string"
      }
    }
  ],
  "TotalResults": 0,
  "ProcessingTimeMs": 0,
  "Success": true,
  "ErrorMessage": null
}
```

### ComplexSearchRequest
```json
{
  "SemanticQuery": "string", // Natural language query (optional)
  "SimilarityReferenceKey": "string", // Reference image path (optional)
  "Limit": 20, // Max results (1-100, default: 20)
  "Threshold": 0.5, // Similarity threshold (0.0-1.0, default: 0.5)
  "CombinationMode": "WeightedCombination", // Union|Intersection|WeightedCombination
  "SemanticWeight": 0.5, // Semantic search weight (0.0-1.0, default: 0.5)
  "SimilarityWeight": 0.5, // Similarity search weight (0.0-1.0, default: 0.5)
  "Filters": {
    "ProjectNames": ["string"], // Optional project filters
    "Years": ["string"], // Optional year filters
    "DirectoryNames": ["string"], // Optional directory filters
    "FileExtensions": ["string"], // Optional file extension filters
    "UploadDateRange": {
      "StartDate": "2024-01-01T00:00:00Z",
      "EndDate": "2024-12-31T23:59:59Z"
    },
    "CustomFilters": {
      "key": "value" // Custom metadata filters
    }
  }
}
```

### ComplexSearchResponse
```json
{
  "Request": {
    // Original request parameters
  },
  "Results": [
    {
      "ObjectKey": "string", // Image path
      "RelevanceScore": 0.85, // Combined relevance score (0.0-1.0)
      "SemanticScore": 0.82, // Semantic similarity score (optional)
      "SimilarityScore": 0.88, // Visual similarity score (optional)
      "MatchedSearchTypes": ["semantic", "similarity"], // Search types that matched
      "FileName": "string",
      "ProjectName": "string",
      "DirectoryName": "string",
      "Year": "string",
      "UploadDate": "2024-01-01T00:00:00Z",
      "ImageUrl": "string",
      "Metadata": {
        "path": "string",
        "project_name": "string"
      }
    }
  ],
  "TotalResults": 0,
  "ProcessingTimeMs": 0,
  "Breakdown": {
    "SemanticResults": 0, // Number of semantic search results
    "SimilarityResults": 0, // Number of similarity search results
    "OverlapResults": 0, // Number of overlapping results
    "SemanticSearchTimeMs": 0, // Time for semantic search
    "SimilaritySearchTimeMs": 0, // Time for similarity search
    "CombinationTimeMs": 0 // Time for combining results
  },
  "Success": true,
  "ErrorMessage": null
}
```

### ImageMetadata
```json
{
  "id": "string",
  "blobUri": "string",
  "uploadedBy": "string",
  "tags": ["string"],
  "description": "string",
  "uploadDate": "2024-01-01T00:00:00.000Z",
  "cameraGeneratedMetadata": {
    // Comprehensive EXIF data including:
    // - Camera make, model, settings
    // - GPS coordinates
    // - Image dimensions
    // - Color profiles
    // - Timestamps
  }
}
```

## Authentication & Security

### JWT Bearer Authentication

All protected endpoints require a JWT token in the Authorization header:

```
Authorization: Bearer <jwt_token>
```

**Token Details:**
- **Issuer**: `loicportraits.azurewebsites.net`
- **Audience**: `loicportraits.azurewebsites.net`
- **Signing**: HMAC SHA-256 with secret key
- **Expiration**: Configurable (check token claims)

### Roles
- **FullAccess**: Required for raw image uploads and administrative operations

### Google OAuth Configuration
- Validate Google ID tokens via Google's API
- Extract user information (email, name, picture)
- Generate custom JWT for API authentication

## Technical Specifications

### File Upload Limits
- **Maximum file size**: 100MB (104,857,600 bytes)
- **Supported formats**: Image files (JPEG, PNG, TIFF, etc.)
- **Archive format**: ZIP files only
- **Directory structure**: Preserved from ZIP archive

### AI/ML Processing
- **Model**: OpenAI CLIP vision transformer (clip-vit-base-patch32)
- **Runtime**: ONNX Runtime for cross-platform compatibility
- **Vector dimensions**: 512 floats
- **Batch processing**: Configurable batch sizes
- **Storage**: Qdrant vector database

### Environment Configuration

Required environment variables:

```bash
# Authentication
JWT_SECRET_KEY=your-jwt-secret-key
VITE_GOOGLE_CLIENT_ID=your-google-client-id

# AI/ML
CLIP_MODEL_PATH=/models/model.onnx
ENABLE_EMBEDDINGS=true

# Storage
MINIO_ENDPOINT=localhost:9000
MINIO_ACCESS_KEY=minioadmin
MINIO_SECRET_KEY=minioadmin

# Vector Database
QDRANT_URL=localhost:6333
QDRANT_COLLECTION=test

# CORS
ALLOWED_ORIGINS=http://localhost:80
```

## Error Handling

### Standard HTTP Status Codes
- **200**: Success
- **400**: Bad Request (validation errors, missing parameters)
- **401**: Unauthorized (invalid/missing JWT token)
- **403**: Forbidden (insufficient permissions)
- **500**: Internal Server Error

### Error Response Format
```json
{
  "Status": 400,
  "Message": "Validation errors occurred",
  "Errors": {
    "fieldName": ["error message 1", "error message 2"]
  }
}
```

### Common Error Scenarios
- **Missing JWT token**: 401 Unauthorized
- **Invalid Google token**: 400 Bad Request
- **File too large**: 400 Bad Request
- **Unsupported file format**: 400 Bad Request
- **Missing required parameters**: 400 Bad Request
- **Project not found**: 400 Bad Request
- **AI service unavailable**: 500 Internal Server Error

## Rate Limiting & Performance

### Upload Performance
- **Concurrent uploads**: Limited by server configuration
- **Processing time**: Depends on image count and AI processing
- **Storage**: Asynchronous background processing
- **Embeddings**: Generated after successful upload

### Search Performance
- **Vector search**: Sub-second response times
- **Result ranking**: Cosine similarity scoring
- **Caching**: API-level caching for frequent queries
- **Pagination**: Planned for large result sets

## Testing the API

### Interactive Documentation
Visit `/swagger` when running the backend to access Swagger UI for interactive API testing.

### Sample Requests

**Get Projects with cURL:**
```bash
curl -X GET "http://localhost:5000/api/image/projects?year=2024" \
  -H "Authorization: Bearer <jwt_token>"
```

**Upload Images with cURL:**
```bash
curl -X POST "http://localhost:5000/api/image/raw" \
  -H "Authorization: Bearer <jwt_token>" \
  -F "timeStamp=2024-01-15T10:30:00" \
  -F "projectName=vacation_photos" \
  -F "directoryFile=@photos.zip"
```

**Google Login with cURL:**
```bash
curl -X POST "http://localhost:5000/api/auth/google-login" \
  -H "Content-Type: application/json" \
  -d '{"Token": "google-id-token-here"}'
```

**Semantic Search with cURL:**
```bash
curl -X GET "http://localhost:5000/api/search/semantic?query=dogs%20playing%20in%20water&limit=10&threshold=0.6" \
  -H "Authorization: Bearer <jwt_token>"
```

**Visual Similarity Search with cURL:**
```bash
curl -X GET "http://localhost:5000/api/search/similarity?objectKey=2024/1705401600/vacation_photos/beach_day/IMG_1234.jpg&limit=10&threshold=0.7" \
  -H "Authorization: Bearer <jwt_token>"
```

**Advanced Similarity Search with cURL:**
```bash
curl -X POST "http://localhost:5000/api/search/similarity" \
  -H "Authorization: Bearer <jwt_token>" \
  -H "Content-Type: application/json" \
  -d '{
    "ObjectKey": "2024/1705401600/vacation_photos/beach_day/IMG_1234.jpg",
    "Limit": 15,
    "Threshold": 0.8,
    "ProjectName": "vacation_photos",
    "Year": "2024"
  }'
```

**Complex Multi-Criteria Search with cURL:**
```bash
curl -X POST "http://localhost:5000/api/search/query" \
  -H "Authorization: Bearer <jwt_token>" \
  -H "Content-Type: application/json" \
  -d '{
    "SemanticQuery": "dogs playing in water",
    "SimilarityReferenceKey": "2024/1705401600/vacation_photos/beach_day/IMG_1234.jpg",
    "Limit": 20,
    "Threshold": 0.6,
    "CombinationMode": "WeightedCombination",
    "SemanticWeight": 0.7,
    "SimilarityWeight": 0.3,
    "Filters": {
      "ProjectNames": ["vacation_photos"],
      "Years": ["2024"],
      "FileExtensions": ["jpg", "png"]
    }
  }'
```

**Semantic-Only Complex Search with cURL:**
```bash
curl -X POST "http://localhost:5000/api/search/query" \
  -H "Authorization: Bearer <jwt_token>" \
  -H "Content-Type: application/json" \
  -d '{
    "SemanticQuery": "sunset landscape mountains",
    "Limit": 10,
    "Threshold": 0.7,
    "Filters": {
      "DirectoryNames": ["landscapes", "nature"],
      "UploadDateRange": {
        "StartDate": "2024-06-01T00:00:00Z",
        "EndDate": "2024-08-31T23:59:59Z"
      }
    }
  }'
```

For more detailed testing scenarios and examples, see the [Setup Guide](setup.md).