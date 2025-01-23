# Terraform backend and provider configuration
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

provider "azurerm" {
  features {}
}

# Virtual Network
resource "azurerm_virtual_network" "vnet" {
  name                = "AzurePhotoFlowVNet"
  location            = var.location
  resource_group_name = var.resource_group_name
  address_space       = ["10.0.0.0/16"]
}

# Subnet
resource "azurerm_subnet" "subnet" {
  name                 = "app_gateway_subnet"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.vnet.name
  address_prefixes     = ["10.0.2.0/24"]
}

# Public IP for Application Gateway
resource "azurerm_public_ip" "pip" {
  name                = "app_gateway_public_ip"
  resource_group_name = var.resource_group_name
  location            = var.location
  allocation_method   = "Static"
}


resource "azurerm_web_application_firewall_policy" "waf_policy" {
  name                = "${var.firewallname}-waf-policy"
  resource_group_name = var.resource_group_name
  location            = var.location

  custom_rules {
    name      = "AllowAll"
    priority  = 100
    rule_type = "MatchRule"

    match_conditions {
      match_variables {
        variable_name = "RequestHeaders"
        selector      = "User-Agent"
      }

      operator = "Contains"
      match_values   = ["*"]
    }

    action = "Allow"
  }

  managed_rules {
    managed_rule_set {
      type    = "OWASP"
      version = "3.2"
    }
  }
}


# Application Gateway Module
module "application_gateway" {
  source              = "./modules/application_gateway"
  name                = "AzurePhotoFlowAG"
  location            = var.location
  resource_group_name = var.resource_group_name
  public_ip_name      = azurerm_public_ip.pip.id
  subnet_id           = azurerm_subnet.subnet.id


	backend_services = [
	  { fqdn = "azurephotoflowfe.azurewebsites.net" },
	  { fqdn = "azurephotoflowbe.azurewebsites.net" }
	]

  ssl_certificate = {
    path     = "./certs/https/aspnetapp.pfx"
    password = var.ssl_certificate_password
  }

  tags = {
    environment = var.environment
  }
}

# Shared service plan for all App Services
resource "azurerm_service_plan" "service_plan" {
  name                = var.service_plan_name
  location            = var.location
  resource_group_name = var.resource_group_name
  os_type             = "Linux"
  sku_name            = "F1" # Free tier for development and testing

  tags = {
    environment = var.environment
    project     = "AzurePhotoFlow"
  }
}

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

# Backend App Service
resource "azurerm_linux_web_app" "backend" {
  name                = var.backend_app_name
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
  }
}

# Frontend App Service
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
  }
}

