variable "location" {
  description = "The location of the storage account"
  default     = "eastus"
}

variable "storage_account_name" {
  description = "The name of the storage account"
  default     = "photoflowtfstatedev"
}

variable "container_name" {
  description = "The name of the container"
  default     = "tfstate"
}

variable "resource_group_name" {
  description = "The name of the resource group"
  default     = "AzurePhotoFlow-RG"
}

