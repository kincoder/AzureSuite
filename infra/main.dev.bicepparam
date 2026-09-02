using 'main.bicep'

param sqlServerName = 'sql-azuresuite-dev-pumpkin'
param sqlAdminLogin = 'sqladmin'
param keyVaultName = 'kv-azsuite-dev-pumpkin'
param principalId = 'dab4a8d0-a4ee-4bbe-bd82-a0b9f87c5aff'
param aadAdminLogin = 'kin.coder_gmail.com#EXT#@kincodergmail.onmicrosoft.com'
param logAnalyticsWorkspaceName = 'log-azuresuite-dev-pumpkin'
param appInsightsName = 'appi-azuresuite-dev-pumpkin'

// Pulled live from Key Vault at deploy time - never stored in this file or in git.
param sqlAdminPassword = az.getSecret('2008745c-a136-400d-8f66-7c9f5fe0939f', 'rg-azuresuite-dev', 'kv-azsuite-dev-pumpkin', 'sql-admin-password')
