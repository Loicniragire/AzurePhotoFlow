#################################
# Existing Variables
#################################
variable "environment" {
  description = "The environment in which the resources are deployed (e.g., dev, staging, prod)"
  default     = "dev"
}

variable "account_replication_type" {
  description = "The type of replication to use for the storage account (e.g., LRS, GRS, ZRS)"
  default     = "LRS"
}

variable "location" {
  description = "The Azure region where resources will be deployed"
  default     = "eastus2"
}

variable "storage_account_name" {
  description = "The name of the storage account for storing Terraform state"
  default     = "photoflowtfstatedev"
}

variable "resource_group_name" {
  description = "The name of the resource group to create or use for the deployment"
  default     = "AzurePhotoFlow-RG"
}

variable "cognitive_account_name" {
  description = "The name of the Azure Cognitive Services account"
  default     = "photoflowcognitive"
}

variable "container_names" {
  description = "A list of container names for Azure Storage"
  type        = list(string)
  default     = ["images", "tfstate"]
}

variable "container_registry_name" {
  description = "The name of the Azure Container Registry (ACR)"
  default     = "AzurePhotoFlowACR"
}

variable "service_plan_name" {
  description = "The name of the Azure App Service Plan"
  default     = "AzurePhotoFlowSP"
}

variable "web_app_name" {
  description = "The name of the Azure App Service hosting the backend API"
  default     = "AzurePhotoFlowWebApp"
}

variable "ssl_certificate_password" {
  description = "The password for the SSL certificate used by the Application Gateway"
  type        = string
  sensitive   = true
}

variable "frontend_web_app_name" {
  type        = string
  description = "Name for the Frontend Azure Web App"
}

variable "backend_web_app_name" {
  type        = string
  description = "Name for the Backend Azure Web App"
}

variable "backend_image_tag" {
  type        = string
  default     = "latest"
  description = "Tag for the backend Docker image"
}

variable "frontend_image_tag" {
  type        = string
  default     = "latest"
  description = "Tag for the frontend Docker image"
}

variable "vite_api_base_url" {
  type        = string
  default     = ""
  description = "API base URL for the frontend"
}


#################################
# Added Variables
#################################
variable "certificate_password" {
  type        = string
  default     = ""
  description = "Password for an optional certificate the application may need"
  sensitive   = true
}

variable "certificate_path" {
  type        = string
  default     = ""
  description = "Path to the certificate within the container or filesystem"
}

variable "azure_blob_storage" {
  type        = string
  default     = ""
  description = "Blob storage connection string or relevant Azure Blob config"
}
variable "metadata_queue" {
  type        = string
  default     = ""
  description = "Queue connection string"
}

variable "docker_registry_username" {
  type        = string
  description = "Container registry Username"
}

variable "docker_registry_password" {
  type        = string
  description = "Password for the Docker registry"
}

variable "jwt_secret_key" {
  type        = string
  default     = ""
  description = "Secret key for JWT token"
}

variable "vite_google_client_id" {
  type        = string
  default     = ""
  description = "Google Client ID for Vite"
}

variable "backend_function_app_name" {
  type        = string
  description = "Name for the Backend Azure Function App"
}

variable "backend_function_image_tag" {
  type        = string
  default     = "latest"
  description = "Tag for the function app Docker image"
}
