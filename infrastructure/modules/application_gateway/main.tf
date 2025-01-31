resource "azurerm_application_gateway" "this" {
  name                = var.name
  location            = var.location
  resource_group_name = var.resource_group_name

  sku {
    name     = "WAF_v2"
    tier     = "WAF_v2"
    capacity = 2
  }

  gateway_ip_configuration {
    name      = "app_gateway_ip_config"
    subnet_id = var.subnet_id
  }

  # Frontend Configuration
  frontend_port {
    name = "https_port"
    port = 443
  }

  frontend_ip_configuration {
    name                 = "frontend_ip"
    public_ip_address_id = var.public_ip_name
  }

  # SSL Certificate (Terminate SSL at Gateway)
  ssl_certificate {
    name     = "ssl_cert"
    data     = filebase64(var.ssl_certificate.path)
    password = var.ssl_certificate.password
  }

  # Listener
  http_listener {
    name                           = "listener_https"
    frontend_ip_configuration_name = "frontend_ip"
    frontend_port_name             = "https_port"
    protocol                       = "Https"
    ssl_certificate_name           = "ssl_cert"
  }

  # Backend Pool (App Service)
  backend_address_pool {
    name = "backend_pool"
    fqdns = [var.app_service_fqdn]
  }

  # Health Probe (Ensure /health exists in your backend)
  probe {
    name                = "health_probe"
    protocol            = "Http"
    host                = var.app_service_fqdn
    path                = "/health"
    interval            = 30
    timeout             = 30
    unhealthy_threshold = 3
  }

  # Backend HTTP Settings
  backend_http_settings {
    name                  = "backend_http_settings"
    port                  = 80
    protocol              = "Http"
    cookie_based_affinity = "Disabled"
    request_timeout       = 60
    probe_name            = "health_probe"
    host_name             = var.app_service_fqdn
  }

  # Routing Rule (Simplified - Nginx handles path-based routing)
  request_routing_rule {
    name               = "default_route"
    rule_type          = "Basic"
    http_listener_name = "listener_https"
    backend_address_pool_name  = "backend_pool"
    backend_http_settings_name = "backend_http_settings"
    priority           = 100  # Lower number = higher priority
  }

  # WAF Configuration
  waf_configuration {
    enabled            = true
    firewall_mode      = "Prevention"
    rule_set_type      = "OWASP"
    rule_set_version   = "3.2"
  }
}

# Configure diagnostics settings for the application gateway
resource "azurerm_monitor_diagnostic_setting" "appgw_diagnostics" {
  name               = "appgw-diagnostics"
  target_resource_id = azurerm_application_gateway.this.id

  # Send logs to Log Analytics Workspace
  log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id

  # Configure which logs to collect
  enabled_log {
    category = "ApplicationGatewayAccessLog"
  }

  enabled_log {
    category = "ApplicationGatewayPerformanceLog"
  }

  enabled_log {
    category = "ApplicationGatewayFirewallLog"
  }

  # Collect metrics
  metric {
    category = "AllMetrics"
  }
}

resource "azurerm_log_analytics_workspace" "main" {
  name                = "agw-logs-workspace"
  location            = var.location
  resource_group_name = var.resource_group_name
  sku                 = "PerGB2018"
  retention_in_days   = 30
}# configure logs and metrics for the application gateway
