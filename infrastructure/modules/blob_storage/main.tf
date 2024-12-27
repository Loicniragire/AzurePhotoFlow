# retrieve existing storage account
data "azurerm_storage_account" "storage" {
  name                     = var.storage_account_name
  resource_group_name      = var.resource_group_name
}

# retrieve existing containers
data "azurerm_storage_container" "containers" {
  for_each				= toset(var.container_names)
  name                  = each.value
  storage_account_name  = data.azurerm_storage_account.storage.name
}

