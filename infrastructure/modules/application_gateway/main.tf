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

  frontend_port {
    name = "https_port"
    port = 443
  }

  frontend_ip_configuration {
    name                 = "frontend_ip"
    public_ip_address_id = var.public_ip_name
  }

  ssl_certificate {
    name     = "ssl_cert"
    data     = filebase64(var.ssl_certificate.path)
    password = var.ssl_certificate.password
  }

  http_listener {
    name                           = "listener_https"
    frontend_ip_configuration_name = "frontend_ip"
    frontend_port_name             = "https_port"
    protocol                       = "Https"
    ssl_certificate_name           = "ssl_cert"
  }

  backend_address_pool {
    name = "backend_pool"

    fqdns = [ var.app_service_fqdn ]
  }

  probe {
    name                = "pf_custom_probe"
    protocol            = "Https"
    path                = "/health"
    interval            = 30
    timeout             = 30
    unhealthy_threshold = 3
    host = var.app_service_fqdn
  }

  backend_http_settings {
    name                  = "http_settings"
    cookie_based_affinity = "Enabled"
    port                  = 443
    protocol              = "Https"
    request_timeout       = 20
    probe_name = "pf_custom_probe"
    host_name = "azurephotoflowwebapp.azurewebsites.net"
  }

  url_path_map {
    name                            = "url_path_map"
    default_backend_address_pool_name = "backend_pool"
    default_backend_http_settings_name = "http_settings"

    path_rule {
      name                       = "api_path"
      paths                      = ["/api/*"]
      backend_address_pool_name  = "backend_pool"
      backend_http_settings_name = "http_settings"
    }

    path_rule {
      name                       = "frontend_path"
      paths                      = ["/*"]
      backend_address_pool_name  = "backend_pool"
      backend_http_settings_name = "http_settings"
    }
  }

  request_routing_rule {
    name                       = "routing_rule"
    rule_type                  = "PathBasedRouting"
    http_listener_name         = "listener_https"
    url_path_map_name          = "url_path_map"
    priority                   = 1
  }

  waf_configuration {
    enabled            = true
    firewall_mode      = "Prevention"
    rule_set_type      = "OWASP"
    rule_set_version   = "3.2"
  }
}

