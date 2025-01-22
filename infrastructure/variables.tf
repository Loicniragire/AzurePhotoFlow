variable "environment" {
  description = "The environment in which the resources are deployed"
  default     = "dev"
}

variable "account_replication_type" {
  description = "The type of replication to use for the storage account"
  default     = "LRS"
}
variable "location" {
  description = "The location of the storage account"
  default     = "eastus2"
}

variable "storage_account_name" {
  description = "The name of the storage account"
  default     = "photoflowtfstatedev"
}

variable "resource_group_name" {
  description = "The name of the resource group"
  default     = "AzurePhotoFlow-RG"
}

variable "cognitive_account_name" {
  description = "The name of the cognitive account"
  default     = "photoflowcognitive"
}

variable "container_names" {
  description = "The names of the containers"
  type        = list(string)
  default     = ["images", "tfstate"]
}

variable "container_registry_name" {
  description = "The name of the container registry"
  default     = "AzurePhotoFlowACR"
}

variable "service_plan_name" {
  description = "The name of the service plan"
  default     = "AzurePhotoFlowSP"
}

variable "backend_app_name" {
  description = "The name of the backend app"
  default     = "AzurePhotoFlowBE"
}

variable "frontend_app_name" {
  description = "The name of the frontend app"
  default     = "AzurePhotoFlowFE"
}

variable "ssl_certificate_password" {
  description = "The password for the SSL certificate used by the Application Gateway"
  type        = string
  sensitive   = true
}

variable "vnet_name" {
  description = "The name of the Virtual Network"
  default     = "AzurePhotoFlowVNet"
}

variable "public_ip" {
  description = "The name of the public Ip"
  default     = "AzurePhotoFlowPip"
}

variable "subnet" {
  description = "The name of the public Ip"
  default     = "AzurePhotoFlowSubNet"
}
