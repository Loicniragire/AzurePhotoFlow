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

output "cosmosdb_connection_string" {
  value = azurerm_cosmosdb_account.db.primary_sql_connection_string
  sensitive = true
}
