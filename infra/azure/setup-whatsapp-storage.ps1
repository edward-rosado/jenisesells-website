# One-time Azure Storage setup for WhatsApp
# Run manually after provisioning the storage account
#
# Usage:
#   .\setup-whatsapp-storage.ps1 -StorageAccountName real-estate-star-storage

param(
    [Parameter(Mandatory=$true)]
    [string]$StorageAccountName
)

# Create queue for webhook processing
az storage queue create --name whatsapp-webhooks --account-name $StorageAccountName

# Create table for audit trail
az storage table create --name whatsappaudit --account-name $StorageAccountName

Write-Host "WhatsApp storage resources created in $StorageAccountName"
