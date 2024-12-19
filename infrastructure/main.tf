# Terraform block for backend configuration and required version
terraform {
  required_version = ">= 1.3.0"

  backend "azurerm" {
    storage_account_name = "your-storage-account-name"  # Replace with actual values
    container_name       = "tfstate"
    key                  = "azurephotoflow.tfstate"
    resource_group_name  = "your-resource-group-name"
    subscription_id      = "your-subscription-id"
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
}

# Module: Computer Vision
module "computer_vision" {
  source               = "./modules/computer_vision"
  cognitive_account_name = var.cognitive_account_name
  resource_group_name  = var.resource_group_name
  location             = var.location
}

# Module: Cognitive Search
module "cognitive_search" {
  source              = "./modules/cognitive_search"
  cognitive_search_name = var.cognitive_search_name
  resource_group_name  = var.resource_group_name
  location             = var.location
}

# Add other modules as needed, such as ML Workspace and Function Apps

