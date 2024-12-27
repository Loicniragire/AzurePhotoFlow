variable "location" {
  description = "the location of the storage account",
  value = "eastus"
}

variable "storage_account_name" {
  description = "The name of the storage account",
  value = "photoflowtfstatedev"
}

variable "container_name" {
  description = "The name of the container",
  value = "tfstate"
}

variable "resource_group_name" {
  description = "The name of the resource group",
  value = "AzurePhotoFlow-RG"
}
