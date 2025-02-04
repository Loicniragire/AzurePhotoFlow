variable "name" {
  description = "The name of the Application Gateway"
  type        = string
}

variable "location" {
  description = "The location/region of the resources"
  type        = string
}

variable "resource_group_name" {
  description = "The name of the Resource Group where the Application Gateway will be deployed"
  type        = string
}

variable "public_ip_id" {
  description = "The ID of the Public IP resource for the Application Gateway"
  type        = string
}

variable "subnet_id" {
  description = "The ID of the subnet where the Application Gateway will be deployed"
  type        = string
}

variable "ssl_certificate" {
  description = "SSL certificate details (path and password)"
  type = object({
    path     = string
    password = string
  })
}

variable "tags" {
  description = "A map of tags to assign to the resources"
  type        = map(string)
}

variable "app_service_fqdn" {
  description = "The FQDN of the App Service to use as the backend pool"
  type        = string
}
