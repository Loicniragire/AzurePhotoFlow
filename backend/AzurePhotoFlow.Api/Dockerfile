# ==================
# 1) Build Stage
# ==================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy project file and restore dependencies
COPY AzurePhotoFlow.Api.csproj .
RUN dotnet restore

# Copy the rest of the files and build the application
COPY . .

# Set build-time environment variables
ARG AZURE_BLOB_STORAGE
ARG CERTIFICATE_PASSWORD
ARG CERTIFICATE_PATH

ENV AZURE_BLOB_STORAGE=${AZURE_BLOB_STORAGE}
ENV CERTIFICATE_PASSWORD=${CERTIFICATE_PASSWORD}
ENV CERTIFICATE_PATH=${CERTIFICATE_PATH}

RUN dotnet publish -c Release -o out

# ==================
# 2) Final Runtime Stage
# ==================
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# (A) Install curl (Debian-based command)
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/out .

# Propagate environment variables
ENV AZURE_BLOB_STORAGE=${AZURE_BLOB_STORAGE}
ENV CERTIFICATE_PASSWORD=${CERTIFICATE_PASSWORD}
ENV CERTIFICATE_PATH=${CERTIFICATE_PATH}

EXPOSE 80
ENTRYPOINT ["dotnet", "AzurePhotoFlow.Api.dll"]
