output "application_gateway_id" {
  value = azurerm_application_gateway.this.id
}

output "public_ip" {
  value       = var.public_ip_name
  description = "The Public IP address for the Application Gateway."
}

