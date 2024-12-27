# retrieve existing storage account
data "azurerm_storage_account" "storage" {
  name                     = var.storage_account_name
  resource_group_name      = var.resource_group_name
}

# Create a blob containter in the existing storage account
resource "azurerm_storage_container" "images" {
  name                  = var.container_name
  storage_account_name  = data.azurerm_storage_account.storage.name
  container_access_type = "private"
}
