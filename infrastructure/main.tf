# Terraform block for backend configuration and required version
terraform {
  required_version = ">= 1.3.0"
	backend "azurerm" {
		storage_account_name = "photoflowtfstatedev"
		container_name       = "tfstate"
		key                  = "terraform.tfstate"
		resource_group_name  = "AzurePhotoFlow-RG"
		subscription_id      = "ebe2acfb-f4a5-4f6b-8f30-252c571813f9"
	  }
}

# Provider Configuration
provider "azurerm" {
  features {}
}

# Module: Blob Storage
module "blob_storage" {
  source              = "./modules/blob_storage"
  storage_account_name = var.storage_account_name
  resource_group_name  = var.resource_group_name
  location             = var.location
  account_replication_type = var.account_replication_type
  account_tier = "Standard"
  delete_retention_days = 7
  container_names = var.container_names
}

# Module: Cognitive Search
module "cognitive_search" {
  source              = "./modules/cognitive_search"
  cognitive_account_name = var.cognitive_account_name
  resource_group_name  = var.resource_group_name
  kind                 = "CognitiveServices" # Enables general purpose APIs
  location             = var.location
  sku_name             = "S0"
}

# Add other modules as needed, such as ML Workspace and Function Apps

