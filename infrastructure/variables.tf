
variable "account_replication_type" {
  description = "The type of replication to use for the storage account"
  default     = "LRS"
}
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
  default     = "images"
}

variable "resource_group_name" {
  description = "The name of the resource group"
  default     = "AzurePhotoFlow-RG"
}

variable "cognitive_account_name" {
  description = "The name of the cognitive account"
  default     = "photoflowcognitive"
}
