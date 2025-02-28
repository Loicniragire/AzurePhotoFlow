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

# Expose the Azure Functions default port
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80

# Start the Azure Functions host
CMD ["dotnet", "AzurePhotoFlow.Functions.dll"]
# CMD ["func", "host", "start", "--verbose"]


