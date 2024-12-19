# main.tf
resource "azurerm_cognitive_account" "vision" {
  name                = var.cognitive_account_name
  location            = var.location
  resource_group_name = var.resource_group_name
  kind                = "ComputerVision"
  sku_name            = "S1"
}
