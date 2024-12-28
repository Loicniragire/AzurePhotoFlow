output "acr_login_server" {
  description = "The login server URL for the Azure Container Registry"
  value       = azurerm_container_registry.acr.login_server
}

output "acr_admin_username" {
  description = "The admin username for the Azure Container Registry"
  value       = azurerm_container_registry.acr.admin_username
}

output "container_registry_login_server" {
  description = "The login server URL for the Azure Container Registry"
  value       = azurerm_container_registry.acr.login_server
}

output "backend_app_service_url" {
  description = "The URL of the backend App Service"
  value       = azurerm_app_service.backend.default_site_hostname
}

output "frontend_app_service_url" {
  description = "The URL of the frontend App Service"
  value       = azurerm_app_service.frontend.default_site_hostname
}
