output "application_gateway_id" {
  value = azurerm_application_gateway.this.id
}

output "public_ip" {
  value = azurerm_public_ip.public_ip.ip_address
}

