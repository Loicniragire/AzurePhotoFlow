#############################################
# 1) Build Stage
#############################################
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Create and switch to a new working directory for the build
WORKDIR /src
# 1) Copy the entire POCO project folder into /src/AzurePhotoFlow.POCO
COPY backend/AzurePhotoFlow.POCO ./AzurePhotoFlow.POCO

# 2) Copy the entire Shared project folder into /src/AzurePhotoFlow.Shared
COPY backend/AzurePhotoFlow.Shared ./AzurePhotoFlow.Shared

# 3) Copy the entire API project folder into /src/AzurePhotoFlow.Api
COPY backend/AzurePhotoFlow.Api ./AzurePhotoFlow.Api

# 3) Switch into the API folder (so restore/publish runs in the right place)
WORKDIR /src/AzurePhotoFlow.Api


# Restore dependencies
RUN dotnet restore AzurePhotoFlow.Api.csproj

# Build-time arguments (only if needed for build logic)
ARG AZURE_BLOB_STORAGE
ARG CERTIFICATE_PASSWORD
ARG CERTIFICATE_PATH
ARG MINIO_ENDPOINT
ARG MINIO_ACCESS_KEY
ARG MINIO_SECRET_KEY
ENV AZURE_BLOB_STORAGE=$AZURE_BLOB_STORAGE
ENV CERTIFICATE_PASSWORD=$CERTIFICATE_PASSWORD
ENV CERTIFICATE_PATH=$CERTIFICATE_PATH
ENV MINIO_ENDPOINT=$MINIO_ENDPOINT
ENV MINIO_ACCESS_KEY=$MINIO_ACCESS_KEY
ENV MINIO_SECRET_KEY=$MINIO_SECRET_KEY

# Publish the application in Release mode to the "out" folder
RUN dotnet publish AzurePhotoFlow.Api.csproj \
    -c Release \
    -o /app/out 


#############################################
# 2) Runtime Stage
#############################################
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# Optionally install debugging tools (curl, etc.) in the final image
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Create working directory for the runtime
WORKDIR /app

# Copy published output from the build stage
COPY --from=build /app/out ./

ENV ASPNETCORE_URLS=http://+:80

EXPOSE 80

# Start the application
ENTRYPOINT ["dotnet", "AzurePhotoFlow.Api.dll"]

