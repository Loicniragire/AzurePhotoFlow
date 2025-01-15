# Use .NET SDK for build and runtime
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy project file and restore dependencies
COPY AzurePhotoFlow.Api.csproj .
RUN dotnet restore

# Copy the rest of the files and build the application
COPY . .
ARG AZURE_BLOB_STORAGE
ENV AZURE_BLOB_STORAGE=${AZURE_BLOB_STORAGE}

ARG CERTIFICATE_PASSWORD
ENV CERTIFICATE_PASSWORD=${CERTIFICATE_PASSWORD}

ARG CERTIFICATE_PATH
ENV CERTIFICATE_PATH=$(CERTIFICATE_PATH)

RUN dotnet publish -c Release -o out

# Use a runtime image for deployment
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# Set runtime environment variables
ARG AZURE_BLOB_STORAGE
ENV AZURE_BLOB_STORAGE=${AZURE_BLOB_STORAGE}

ARG CERTIFICATE_PASSWORD
ENV CERTIFICATE_PASSWORD=${CERTIFICATE_PASSWORD}

ARG CERTIFICATE_PATH
ENV CERTIFICATE_PATH=$(CERTIFICATE_PATH)

# Expose port and start the application
EXPOSE 80
ENTRYPOINT ["dotnet", "AzurePhotoFlow.Api.dll"]

