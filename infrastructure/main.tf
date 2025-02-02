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

  app_service_fqdn = azurerm_linux_web_app.web_app.default_hostname

  ssl_certificate = {
    path     = "./certs/myserver.pfx"
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
    always_on        = true

    # Allow Application Gateway IP
    ip_restriction {
      ip_address = "${azurerm_public_ip.pip.ip_address}/32"
      name       = "Allow-AppGW"
      priority   = 100
      action     = "Allow"
    }

    # Allow Azure Load Balancer (Required for health probes)
    # ip_restriction {
    #   service_tag = "AzureLoadBalancer"
    #   name        = "Allow-LoadBalancer"
    #   priority    = 200
    #   action      = "Allow"
    # }
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

# resource "azurerm_application_insights" "app_insights" {
#   name                = "pf-app-insights"
#   location            = var.location
#   resource_group_name = var.resource_group_name
#   application_type    = "web"
#   workspace_id        = "DefaultWorkspace-ebe2acfb-f4a5-4f6b-8f30-252c571813f9-EUS2"
# }


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

# Connect the Application Gateway to the Log Analytics workspace
resource "azurerm_monitor_diagnostic_setting" "appgw_diagnostics" {
  name                       = "appgw-diagnostics"
  target_resource_id         = module.application_gateway.application_gateway_id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.log_workspace.id

  enabled_log { category = "ApplicationGatewayAccessLog" }
  enabled_log { category = "ApplicationGatewayPerformanceLog" }
  enabled_log { category = "ApplicationGatewayFirewallLog" }
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
