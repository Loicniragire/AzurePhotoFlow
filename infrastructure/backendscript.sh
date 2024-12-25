az group create --name AzurePhotoFlow-RG --location eastus

az storage account create \
  --name photoflowtfstatedev \
  --resource-group AzurePhotoFlow-RG \
  --location eastus \
  --sku Standard_LRS

az storage container create \
  --name images \
  --account-name photoflowtfstatedev

az storage container create \
  --name tfstate \
  --account-name photoflowtfstatedev

