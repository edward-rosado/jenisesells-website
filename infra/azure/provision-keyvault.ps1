# Provision Azure Key Vault and Blob Storage for Data Protection API
# Run once during initial infrastructure setup
#
# Prerequisites:
#   - Azure CLI installed and logged in
#   - real-estate-star-rg resource group exists
#   - real-estate-star-api Container App has system-assigned managed identity

param(
    [string]$ResourceGroup = "real-estate-star-rg",
    [string]$Location = "eastus",
    [string]$StorageAccount = "realestatestarsa",
    [string]$KeyVaultName = "real-estate-star-kv",
    [string]$ContainerAppName = "real-estate-star-api"
)

Write-Host "Creating storage account..." -ForegroundColor Cyan
az storage account create -n $StorageAccount -g $ResourceGroup -l $Location --sku Standard_LRS --min-tls-version TLS1_2

Write-Host "Creating blob container for Data Protection key ring..." -ForegroundColor Cyan
az storage container create -n dataprotection --account-name $StorageAccount

Write-Host "Creating Key Vault..." -ForegroundColor Cyan
az keyvault create -n $KeyVaultName -g $ResourceGroup -l $Location --enable-rbac-authorization

Write-Host "Creating Data Protection key..." -ForegroundColor Cyan
az keyvault key create --vault-name $KeyVaultName -n dataprotection --kty RSA --size 2048

Write-Host "Enabling managed identity on Container App..." -ForegroundColor Cyan
az containerapp identity assign -n $ContainerAppName -g $ResourceGroup --system-assigned

$principalId = az containerapp identity show -n $ContainerAppName -g $ResourceGroup --query principalId -o tsv

$subId = az account show --query id -o tsv

Write-Host "Assigning Storage Blob Data Contributor..." -ForegroundColor Cyan
az role assignment create --assignee $principalId --role "Storage Blob Data Contributor" --scope "/subscriptions/$subId/resourceGroups/$ResourceGroup/providers/Microsoft.Storage/storageAccounts/$StorageAccount"

Write-Host "Assigning Key Vault Crypto Service Encryption User..." -ForegroundColor Cyan
az role assignment create --assignee $principalId --role "Key Vault Crypto Service Encryption User" --scope "/subscriptions/$subId/resourceGroups/$ResourceGroup/providers/Microsoft.KeyVault/vaults/$KeyVaultName"

Write-Host ""
Write-Host "Done! Set these env vars on the Container App:" -ForegroundColor Green
Write-Host "  AzureKeyVault__VaultUri=https://$KeyVaultName.vault.azure.net"
Write-Host "  DataProtection__BlobUri=https://$StorageAccount.blob.core.windows.net/dataprotection/keys.xml"
