# Use .NET SDK for build and runtime
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy project files
COPY AzurePhotoFlow.Api.csproj .
RUN dotnet restore

# Copy and build the app
COPY . .
ARG AZURE_BLOB_STORAGE
ENV AZURE_BLOB_STORAGE=${AZURE_BLOB_STORAGE}
RUN dotnet publish -c Release -o out

# Use a runtime image for deployment
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/out .

# Expose port and start the application
EXPOSE 80
ENTRYPOINT ["dotnet", "AzurePhotoFlow.Api.dll"]

