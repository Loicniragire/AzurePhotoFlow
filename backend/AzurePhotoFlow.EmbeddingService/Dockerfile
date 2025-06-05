#############################################
# 1) Build Stage
#############################################
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Create and switch to a new working directory for the build
WORKDIR /src

# Copy the POCO project
COPY backend/AzurePhotoFlow.POCO ./AzurePhotoFlow.POCO

# Copy the EmbeddingService project
COPY backend/AzurePhotoFlow.EmbeddingService ./AzurePhotoFlow.EmbeddingService

# Switch into the EmbeddingService folder
WORKDIR /src/AzurePhotoFlow.EmbeddingService

# Restore dependencies
RUN dotnet restore AzurePhotoFlow.EmbeddingService.csproj

# Copy the rest of the source code
COPY . .

# Build-time arguments
ARG QDRANT_URL
ARG QDRANT_COLLECTION
ARG CLIP_MODEL_PATH
ARG MINIO_ENDPOINT
ARG MINIO_ACCESS_KEY
ARG MINIO_SECRET_KEY
ENV QDRANT_URL=$QDRANT_URL
ENV QDRANT_COLLECTION=$QDRANT_COLLECTION
ENV CLIP_MODEL_PATH=$CLIP_MODEL_PATH
ENV MINIO_ENDPOINT=$MINIO_ENDPOINT
ENV MINIO_ACCESS_KEY=$MINIO_ACCESS_KEY
ENV MINIO_SECRET_KEY=$MINIO_SECRET_KEY

# Publish the application
RUN dotnet publish backend/AzurePhotoFlow.EmbeddingService/AzurePhotoFlow.EmbeddingService.csproj \
    -c Release \
    -o /app/out

#############################################
# 2) Runtime Stage
#############################################
FROM mcr.microsoft.com/dotnet/aspnet:8.0

RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/out ./

ENV ASPNETCORE_URLS=http://+:80

EXPOSE 80

ENTRYPOINT ["dotnet", "AzurePhotoFlow.EmbeddingService.dll"]
