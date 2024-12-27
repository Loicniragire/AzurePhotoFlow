output "cognitive_account_name" {
  description = "The name of the Azure Cognitive Services account"
  value       = azurerm_cognitive_account.cognitive.name
}

output "cognitive_account_endpoint" {
  description = "The endpoint URL for the Azure Cognitive Services account"
  value       = azurerm_cognitive_account.cognitive.endpoint
}

output "cognitive_account_primary_key" {
  description = "The primary key for the Azure Cognitive Services account"
  value       = azurerm_cognitive_account.cognitive.primary_access_key
}
