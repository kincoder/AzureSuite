using 'main.bicep'

param sqlAdminLogin = 'sqladmin'
param sqlAdminPassword = az.getSecret('2008745c-a136-400d-8f66-7c9f5fe0939f', 'rg-messaginghub-dev', 'kv-msghub-catalog-dev', 'sql-admin-password')
param principalId = 'dab4a8d0-a4ee-4bbe-bd82-a0b9f87c5aff'
