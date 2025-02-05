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

# Shared service plan for all App Services
resource "azurerm_service_plan" "service_plan" {
  name                = var.service_plan_name
  location            = var.location
  resource_group_name = var.resource_group_name
  os_type             = "Linux"
  sku_name            = "B1" # Minimum required for containers

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

# Unified App Service for Frontend and Backend
resource "azurerm_linux_web_app" "web_app" {
  name                = var.web_app_name
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
    #App insights
    # "APPINSIGHTS_INSTRUMENTATIONKEY" = azurerm_application_insights.app_insights.instrumentation_key
    # "APPINSIGHTS_CONNECTION_STRING"  = azurerm_application_insights.app_insights.connection_string
  }

  tags = {
    environment = var.environment
    project     = "AzurePhotoFlow"
  }
}

resource "azurerm_role_assignment" "app_service_acr_pull" {
  scope                = azurerm_container_registry.acr.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_linux_web_app.web_app.identity[0].principal_id
}


resource "azurerm_log_analytics_workspace" "log_workspace" {
  name                = "pf-log-analytics"
  resource_group_name = var.resource_group_name
  location            = var.location
  sku                 = "PerGB2018"
  retention_in_days   = 30

  tags = {
    environment = var.environment
    project     = "AzurePhotoFlow"
  }
}

# Connect the App Service to the Log Analytics workspace
resource "azurerm_monitor_diagnostic_setting" "webapp_diagnostics" {
  name                       = "webapp-diagnostics"
  target_resource_id         = azurerm_linux_web_app.web_app.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.log_workspace.id

  enabled_log { category = "AppServiceAppLogs" }
  enabled_log { category = "AppServiceAuditLogs" }
  enabled_log { category = "AppServiceHTTPLogs" }
  metric { category = "AllMetrics" }
}


data "azurerm_storage_account" "storage" {
  name                = "photoflowtfstatedev"
  resource_group_name = var.resource_group_name
}

resource "azurerm_monitor_diagnostic_setting" "storage_diagnostics" {
  name                       = "storage-diagnostics"
  target_resource_id         = "${data.azurerm_storage_account.storage.id}/blobServices/default"
  log_analytics_workspace_id = azurerm_log_analytics_workspace.log_workspace.id

  enabled_log { category = "StorageRead" }
  enabled_log { category = "StorageWrite" }
  enabled_log { category = "StorageDelete" }
}
