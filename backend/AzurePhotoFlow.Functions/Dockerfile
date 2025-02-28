# Stage 1: Build the .NET application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy the project file and restore dependencies
COPY AzurePhotoFlow.Functions.csproj .
RUN dotnet restore

# Copy the remaining application files and build the app
COPY . .
RUN dotnet publish -c Release -o /out

# Stage 2: Runtime image with Azure Functions
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0
WORKDIR /home/site/wwwroot

# Copy built application from the build stage
COPY --from=build /out .

# Set required environment variables
ENV FUNCTIONS_WORKER_RUNTIME=dotnet-isolated
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV GRPC_WORKER__GRPC_LISTEN="0.0.0.0:50051"
ENV FUNCTIONS__WORKER__HOSTENDPOINT=http://0.0.0.0:50051
ENV FUNCTIONS__WORKER__ID=worker-1
# ENV GRPC_WORKER__WORKER_ID=worker-1

# Expose the correct gRPC port
EXPOSE 50051

# Start the Azure Functions host
ENTRYPOINT ["dotnet", "AzurePhotoFlow.Functions.dll"]

