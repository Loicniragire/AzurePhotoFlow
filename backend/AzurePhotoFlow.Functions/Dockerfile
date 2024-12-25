# Use .NET SDK for build and runtime
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy project files
COPY AzurePhotoFlow.Functions.csproj .
RUN dotnet restore

# Copy and build the app
COPY . .
RUN dotnet publish -c Release -o out

# Use Azure Functions runtime image
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4.0
WORKDIR /app
COPY --from=build /app/out .

# Expose the Azure Functions port
EXPOSE 80
CMD ["dotnet", "AzurePhotoFlow.Functions.dll"]

