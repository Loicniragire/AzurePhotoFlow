resource "azurerm_storage_account" "storage" {
  name                     = var.storage_account_name
  resource_group_name      = var.resource_group_name
  location                 = var.location
  account_tier             = var.account_tier
  account_replication_type = var.account_replication_type

  blob_properties {
    container_delete_retention_policy {
      days = var.delete_retention_days
    }
  }
}

resource "azurerm_storage_container" "images" {
  name                  = var.container_name
  storage_account_name  = azurerm_storage_account.storage.name
  container_access_type = "private"
}
