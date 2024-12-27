resource "azurerm_cognitive_account" "cognitive" {
  name                = var.cognitive_account_name
  location            = var.location
  resource_group_name = var.resource_group_name
  kind                = var.kind
  sku_name            = var.sku_name

  # Enable Computer Vision capability
  capabilities{ name = "ComputerVision" }

  # Enable Face capability
  capabilities{ name = "Face" }

  # Enable Cognitive Search capability
  capabilities{ name = "CognitiveSearch" }

  tags = {
	environment = "dev"
    project = "AzurePhotoFlow"
  }
}
