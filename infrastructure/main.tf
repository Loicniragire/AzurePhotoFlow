terraform {
  required_version = ">= 1.3.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "4.17.0"
    }
  }
  
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
  # sku_name            = "F1"
  sku_name            = "B1"

  tags = {
    environment = var.environment
    project     = "AzurePhotoFlow"
  }
}

###############################
# Frontend Web App
###############################
resource "azurerm_linux_web_app" "frontend_web_app" {
  name                = var.frontend_web_app_name
  location            = var.location
  resource_group_name = var.resource_group_name
  service_plan_id     = azurerm_service_plan.service_plan.id
  https_only = true

  # Enable system-assigned managed identity so it can pull from ACR without credentials
  identity {
    type = "SystemAssigned"
  }

  # Configure various types of logs
  logs {
    # Optional: Detailed error messages
    detailed_error_messages = true

    # Optional: Failed request tracing
    failed_request_tracing = true

    # Optional: Application logs to file system
    application_logs {
      file_system_level = "Verbose" # or "Off", "Verbose", "Information", "Warning"
    }

    # Optional: HTTP logs to file system
    http_logs {
      file_system {
        retention_in_days = 7
        retention_in_mb   = 100
      }
    }
  }

  site_config {
    always_on = false
    application_stack {
      docker_image_name = "azurephotoflow-frontend:${var.frontend_image_tag}"
      docker_registry_url = "https://${azurerm_container_registry.acr.login_server}"
      docker_registry_username = var.docker_registry_username
      docker_registry_password = var.docker_registry_password
    }
    # any other permissible site_config attributes
  }

  # App settings, environment variables, etc. for the frontend
  app_settings = {
    "VITE_API_BASE_URL" = var.vite_api_base_url
    "VITE_GOOGLE_CLIENT_ID" = var.vite_google_client_id
    # ...
  }

  tags = {
    environment = var.environment
    project     = "AzurePhotoFlow"
  }
}

###############################
# Backend Web App
###############################
resource "azurerm_linux_web_app" "backend_web_app" {
  name                = var.backend_web_app_name
  location            = var.location
  resource_group_name = var.resource_group_name
  service_plan_id     = azurerm_service_plan.service_plan.id

  identity {
    type = "SystemAssigned"
  }

  # Configure various types of logs
  logs {
    # Optional: Detailed error messages
    detailed_error_messages = true

    # Optional: Failed request tracing
    failed_request_tracing = true

    # Optional: Application logs to file system
    application_logs {
      file_system_level = "Verbose" # or "Off", "Verbose", "Information", "Warning"
    }

    # Optional: HTTP logs to file system
    http_logs {
      file_system {
        retention_in_days = 7
        retention_in_mb   = 100
      }
    }
  }

  site_config {
    always_on = false
    application_stack {
        docker_image_name = "azurephotoflow-backend:${var.backend_image_tag}"
        docker_registry_url = "https://${azurerm_container_registry.acr.login_server}"
        docker_registry_username = var.docker_registry_username
        docker_registry_password = var.docker_registry_password
    }
    cors {
      allowed_origins     = ["https://${var.frontend_web_app_name}.azurewebsites.net"]
      support_credentials = true
    }
  }

  app_settings = {
    "ASPNETCORE_ENVIRONMENT" = "Production"
    "AZURE_BLOB_STORAGE" = var.azure_blob_storage
    "JWT_SECRET_KEY" = var.jwt_secret_key
    "VITE_GOOGLE_CLIENT_ID" = var.vite_google_client_id
    # ...
  }
}

resource "azurerm_role_assignment" "backend_acr_pull" {
  scope                = azurerm_container_registry.acr.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_linux_web_app.backend_web_app.identity[0].principal_id
}

# The frontend web app needs permission to pull images from ACR
resource "azurerm_role_assignment" "frontend_acr_pull" {
  scope                = azurerm_container_registry.acr.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_linux_web_app.frontend_web_app.identity[0].principal_id
}

resource "azurerm_role_assignment" "function_app_acr_pull" {
  scope                = azurerm_container_registry.acr.id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_linux_function_app.backend_function_app.identity[0].principal_id
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

resource "azurerm_application_insights" "app_insights" {
  name                = "pf-app-insights"
  location            = var.location
  resource_group_name = var.resource_group_name
  application_type    = "web"
  workspace_id        = azurerm_log_analytics_workspace.log_workspace.id
}

# Diagnostic for Frontend Web App
resource "azurerm_monitor_diagnostic_setting" "frontend_webapp_diagnostics" {
  name                       = "frontend-webapp-diagnostics"
  target_resource_id         = azurerm_linux_web_app.frontend_web_app.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.log_workspace.id

  enabled_log { category = "AppServiceAppLogs" }
  enabled_log { category = "AppServiceAuditLogs" }
  enabled_log { category = "AppServiceHTTPLogs" }
  metric      { category = "AllMetrics" }
}

# Diagnostic for Backend Web App
resource "azurerm_monitor_diagnostic_setting" "backend_webapp_diagnostics" {
  name                       = "backend-webapp-diagnostics"
  target_resource_id         = azurerm_linux_web_app.backend_web_app.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.log_workspace.id

  enabled_log { category = "AppServiceAppLogs" }
  enabled_log { category = "AppServiceAuditLogs" }
  enabled_log { category = "AppServiceHTTPLogs" }
  metric      { category = "AllMetrics" }
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


resource "azurerm_monitor_diagnostic_setting" "function_app_diagnostics" {
  name                       = "function-app-diagnostics"
  target_resource_id         = azurerm_linux_function_app.backend_function_app.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.log_workspace.id

  enabled_log { category = "FunctionAppLogs"}
  metric { category = "AllMetrics"}
}


resource "azurerm_storage_queue" "queue" {
  name                 = var.metadata_queue
  storage_account_name = data.azurerm_storage_account.storage.name
}

resource "azurerm_cosmosdb_account" "db" {
  name                = "loicportraits-cosmosdb"
  location            = var.location
  resource_group_name = var.resource_group_name
  offer_type          = "Standard"
  kind                = "GlobalDocumentDB"
  consistency_policy {
    consistency_level = "Session"
  }
  geo_location {
    location          = var.location
    failover_priority = 0
  }
}


resource "azurerm_linux_function_app" "backend_function_app" {
  name                       = var.backend_function_app_name
  resource_group_name        = var.resource_group_name
  location                   = var.location
  service_plan_id            = azurerm_service_plan.service_plan.id
  storage_account_name       = data.azurerm_storage_account.storage.name
  storage_account_access_key = data.azurerm_storage_account.storage.primary_access_key

  identity {
    type = "SystemAssigned"
  }

  site_config {
    always_on = true
    application_insights_connection_string = azurerm_application_insights.app_insights.connection_string
    application_stack {
      docker{
        registry_url = "https://${azurerm_container_registry.acr.login_server}"
        image_name = "azurephotoflow-function"
        image_tag = var.backend_function_image_tag
        registry_username = var.docker_registry_username
        registry_password = var.docker_registry_password
      }
    }
  }

  app_settings = {
    WEBSITES_PORT            = "80"
    AzureWebJobsStorage = data.azurerm_storage_account.storage.primary_connection_string
    CosmosDBConnectionString = azurerm_cosmosdb_account.db.primary_sql_connection_string
    APPLICATIONINSIGHTS_CONNECTION_STRING = azurerm_application_insights.app_insights.connection_string
    DOCKER_ENABLE_LOGGING  = "true"
    WEBSITES_ENABLE_APP_SERVICE_STORAGE = "false"
    SCM_DO_BUILD_DURING_DEPLOYMENT = "true"
    FUNCTIONS_WORKER_RUNTIME = "dotnet-isolated"
    
    # Runtime configuration
    ASPNETCORE_URLS = "http://+:80"
    DOTNET_ENVIRONMENT = "Production"
    # Add any additional application settings here.
  }

  tags = {
    environment = var.environment
    project     = "AzurePhotoFlow"
  }
}
