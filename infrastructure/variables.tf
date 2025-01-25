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

variable "vnet_name" {
  description = "The name of the Virtual Network (VNet)"
  default     = "AzurePhotoFlowVNet"
}

variable "subnet_name" {
  description = "The name of the subnet in the Virtual Network"
  default     = "AzurePhotoFlowSubNet"
}

variable "public_ip_name" {
  description = "The name of the Public IP resource for the Application Gateway"
  default     = "AzurePhotoFlowPip"
}

variable "firewallname" {
  description = "Firewall name"
  default     = "AzurePhotoFlowFirewall"
}

variable "app_service_fqdn" {
  description = "The FQDN of the App Service to use as the backend pool"
  type        = string
}
