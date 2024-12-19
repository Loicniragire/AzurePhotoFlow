# Use a Terraform image
FROM hashicorp/terraform:1.5.0
WORKDIR /terraform

# Copy Terraform files
COPY . .

# Set up entrypoint for running Terraform commands
ENTRYPOINT ["terraform"]

