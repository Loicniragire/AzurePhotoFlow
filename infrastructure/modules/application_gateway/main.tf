
resource "azurerm_virtual_network" "vnet" {
  name                = var.vnet_name
  location            = var.location
  resource_group_name = var.resource_group_name
  address_space       = ["10.0.0.0/16"]

  tags = {
    environment = var.environment
  }

resource "azurerm_public_ip" "pip" {
  name                = var.public_ip
  resource_group_name = var.resource_group_name
  location            = var.location
  allocation_method   = "Static"
}

resource "azurerm_subnet" "subnet" {
  name                 = var.subnet
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.vnet.name
  address_prefixes     = ["10.254.0.0/24"]
}




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
    subnet_id = azurerm_subnet.subnet.id
  }

  frontend_port {
    name = "http_port"
    port = 80
  }

  frontend_port {
    name = "https_port"
    port = 443
  }

  frontend_ip_configuration {
    name                 = "frontend_ip"
    public_ip_address_id = azurerm_public_ip.public_ip.id
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

  # Dynamically generate backend pools with backend addresses
  dynamic "backend_address_pool" {
    for_each = var.backend_services
    content {
      name = "backend_pool_${backend_address_pool.key}"

      backend_addresses {
        fqdn = backend_address_pool.value.fqdn
      }
    }
  }

  backend_http_settings {
    name                  = "http_settings"
    cookie_based_affinity = "Enabled"
    port                  = 80
    protocol              = "Http"
    request_timeout       = 20
  }

  url_path_map {
    name                           = "url_path_map"
    default_backend_address_pool_name = "backend_pool_1"
    default_backend_http_settings_name = "http_settings"

    path_rule {
      name                       = "api_path"
      paths                      = ["/api/*"]
      backend_address_pool_name  = "backend_pool_1"
      backend_http_settings_name = "http_settings"
    }
  }

  request_routing_rule {
    name                       = "routing_rule"
    rule_type                  = "PathBasedRouting"
    http_listener_name         = "listener_https"
    url_path_map_name          = "url_path_map"
  }

  tags = var.tags
}
