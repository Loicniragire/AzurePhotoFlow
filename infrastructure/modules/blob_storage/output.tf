output "storage_account_name" {
  value = data.azurerm_storage_account.storage.name
}
output "images_primary_blob_endpoint" {
  value = data.azurerm_storage_account.storage.primary_blob_endpoint
}

output "storage_container_names" {
  value = [for container in azurerm_storage_container.container : container.name]
}


