variable "name" {}
variable "location" {}
variable "resource_group_name" {}
variable "public_ip_name" {}
variable "public_ip_location" {}
variable "subnet_name" {}
variable "subnet_prefix" {
  type = list(string)
}
variable "vnet_name" {}
variable "backend_services" {
  type = list(object({
    fqdn = string
  }))
}
variable "ssl_certificate" {
  type = object({
    path     = string
    password = string
  })
}
variable "tags" {
  type = map(string)
}

variable "backend_services" {
  description = "List of backend services with FQDNs"
  type = list(object({
    fqdn = string
  }))
}


