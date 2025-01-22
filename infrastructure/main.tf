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

# Module: Application Gateway
module "application_gateway" {
  source              = "./modules/application_gateway"
  name                = "AzurePhotoFlowAG"
  location            = var.location
  resource_group_name = var.resource_group_name

  public_ip_name      = "app_gateway_public_ip"
  public_ip_location  = var.location

  subnet_name         = "app_gateway_subnet"
  subnet_prefix       = ["10.0.2.0/24"]
  vnet_name           = azurerm_virtual_network.vnet.name

  backend_services = [
    {
      fqdn = azurerm_linux_web_app.backend.default_hostname
    },
    {
      fqdn = azurerm_linux_web_app.frontend.default_hostname
    }
  ]

  ssl_certificate = {
    path     = "../../../backend/AzurePhotoFlow.Api/certs/https/aspnetapp.pfx"
    password = var.ssl_certificate_password
  }

  tags = {
    environment = var.environment
  }
}

# Add other modules as needed, such as ML Workspace and Function Apps


# Container Registry
resource "azurerm_container_registry" "acr" {
  name                = var.container_registry_name
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = "Basic"
  admin_enabled       = true

  tags = {
	environment = var.environment
    project     = "AzurePhotoFlow"
  }
}


# Shared service plan for all App Services
resource "azurerm_service_plan" "service_plan" {
  name                = var.service_plan_name
  location            = "eastus2"
  resource_group_name = var.resource_group_name
  os_type             = "Linux"
  sku_name			  = "F1"

  tags = {
    environment = var.environment
    project     = "AzurePhotoFlow"
  }
}

# Backend App Service
resource "azurerm_linux_web_app" "backend" {
  name                = var.backend_app_name
  location            = "eastus2"
  resource_group_name = var.resource_group_name
  service_plan_id 	  = azurerm_service_plan.service_plan.id

  identity {
    type = "SystemAssigned"
  }

  site_config {
    app_command_line = ""
	always_on = false # must be set to false when using F1 Service Plan
  }

  app_settings = {
    WEBSITES_ENABLE_APP_SERVICE_STORAGE = "false" # typical for containerized apps, container handles storage
  }

  tags = {
    environment = var.environment
    project     = "AzurePhotoFlow"
  }
}



# Add Managed Identity to Frontend App Service
resource "azurerm_linux_web_app" "frontend" {
  name                = var.frontend_app_name
  location            = var.location
  resource_group_name = var.resource_group_name
  service_plan_id     = azurerm_service_plan.service_plan.id

  identity {
    type = "SystemAssigned"
  }

  site_config {
    app_command_line = ""
    always_on        = false
  }

  app_settings = {
    WEBSITES_ENABLE_APP_SERVICE_STORAGE = "false"
  }

  tags = {
    environment = var.environment
    project     = "AzurePhotoFlow"
  }
}
