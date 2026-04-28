// SkyCMS Azure Infrastructure - Main Bicep Template
// Orchestrates deployment of Editor (Azure SQL + App Service) with optional Publisher (Blob Storage) and Email (ACS)

targetScope = 'resourceGroup'

// ============================================================================
// PARAMETERS
// ============================================================================

@metadata({
  description: 'Location for all resources'
  'x-ms-visibility': 'hidden'
})
param location string = resourceGroup().location

@description('Base name for resources (used to generate unique names)')
@minLength(3)
@maxLength(10)
param baseName string = 'skycms'

@description('Environment name (dev, staging, prod)')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environment string = 'dev'

@description('Deploy publisher (Blob Storage for static website)')
param deployPublisher bool = true

@description('Deploy Azure Communication Services for email')
param deployEmail bool = false

@description('Deploy Application Insights for monitoring')
param deployAppInsights bool = false

@description('Administrator email address for the CMS')
param adminEmail string = ''

// Docker Image Configuration
@description('Docker image for the SkyCMS Editor')
param dockerImage string = 'toiyabe/sky-editor:latest'

@description('Minimum worker instances for App Service Plan')
@minValue(1)
@maxValue(10)
param minReplicas int = 1

@description('Deploy staging deployment slot for zero-downtime deployments')
param deploySlot bool = true

// Tags
@description('Tags to apply to all resources')
param tags object = {
  Application: 'SkyCMS'
  Environment: environment
  ManagedBy: 'Bicep'
}

// ============================================================================
// VARIABLES
// ============================================================================

var uniqueSuffix = uniqueString(resourceGroup().id, baseName)
var uniqueSuffix8 = substring(uniqueSuffix, 0, 8)

var sqlServerName = 'sql-${baseName}-${uniqueSuffix8}'
var appServicePlanName = 'plan-${baseName}-${environment}-${uniqueSuffix8}'
var webAppName = 'editor-${baseName}-${environment}-${uniqueSuffix8}'
var managedIdentityName = 'id-${baseName}-${environment}-${uniqueSuffix8}'
var keyVaultName = 'kv-${baseName}-${uniqueSuffix8}'
var storageAccountName = 'st${substring(baseName, 0, min(10, length(baseName)))}${substring(uniqueSuffix, 0, 10)}'
var communicationServiceName = 'acs-${baseName}-${uniqueSuffix8}'
var appInsightsName = 'ai-${baseName}-${environment}-${uniqueSuffix8}'
var logAnalyticsName = 'law-${baseName}-${environment}-${uniqueSuffix8}'
var databaseName = 'skycms'
var sqlServerHostnameSuffix = az.environment().suffixes.sqlServerHostname
var sqlServerFqdn = startsWith(sqlServerHostnameSuffix, '.')
  ? '${sqlServerName}${sqlServerHostnameSuffix}'
  : '${sqlServerName}.${sqlServerHostnameSuffix}'
var passwordSeed = uniqueString(resourceGroup().id, baseName, 'password')
var generatedAdminUsername = 'admin${substring(uniqueSuffix, 0, 6)}'
var generatedAdminPassword = '${toUpper(substring(passwordSeed, 0, 4))}${substring(passwordSeed, 4, 4)}!${substring(passwordSeed, 8, 4)}${substring(passwordSeed, 12, 1)}'

// ============================================================================
// MANAGED IDENTITY
// ============================================================================

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
  tags: tags
}

// ============================================================================
// KEY VAULT
// ============================================================================

module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault-deployment'
  params: {
    location: location
    keyVaultName: keyVaultName
    principalId: managedIdentity.properties.principalId
    enablePurgeProtection: environment == 'prod'
    tags: tags
  }
}

// ============================================================================
// AZURE SQL DATABASE
// ============================================================================

module sql 'modules/sqlDatabase.bicep' = {
  name: 'sql-deployment'
  params: {
    location: location
    serverName: sqlServerName
    administratorLogin: generatedAdminUsername
    administratorPassword: generatedAdminPassword
    databaseName: databaseName
    tags: tags
  }
}

// ============================================================================
// DATABASE CONNECTION STRING
// ============================================================================

var sqlConnectionString = 'Server=${sqlServerFqdn};Database=${databaseName};User ID=${generatedAdminUsername};Password=${generatedAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'

// ============================================================================
// KEY VAULT SECRETS
// ============================================================================

resource kvDbConnectionString 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${keyVaultName}/ApplicationDbContextConnection'
  properties: {
    value: sqlConnectionString
  }
  dependsOn: [
    keyVault
  ]
}

resource kvStorageConnectionString 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (deployPublisher) {
  name: '${keyVaultName}/StorageConnectionString'
  properties: {
    #disable-next-line BCP318
    value: deployPublisher ? storage.outputs.primaryConnectionString : ''
  }
  dependsOn: [
    keyVault
  ]
}

resource kvAcsConnectionString 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (deployEmail) {
  name: '${keyVaultName}/AzureCommunicationConnection'
  properties: {
    #disable-next-line BCP318
    value: deployEmail ? acs.outputs.connectionString : ''
  }
  dependsOn: [
    keyVault
  ]
}

resource kvAppInsightsConnectionString 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (deployAppInsights) {
  name: '${keyVaultName}/AppInsightsConnectionString'
  properties: {
    #disable-next-line BCP318
    value: deployAppInsights ? appInsights.outputs.connectionString : ''
  }
  dependsOn: [
    keyVault
  ]
}

// ============================================================================
// BLOB STORAGE (PUBLISHER) - Optional
// ============================================================================

module storage 'modules/storage.bicep' = if (deployPublisher) {
  name: 'storage-deployment'
  params: {
    location: location
    storageAccountName: storageAccountName
    skuName: 'Standard_LRS'
    enableStaticWebsite: true
    tags: tags
  }
}

// Reference storage account for scoped role assignment
resource storageAccountExisting 'Microsoft.Storage/storageAccounts@2023-01-01' existing = if (deployPublisher) {
  name: storageAccountName
}

// Grant Managed Identity Storage Blob Data Contributor role to storage account
resource storageBlobDataContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (deployPublisher) {
  // Deterministic name so re-deployments upsert the same role assignment
  name: guid(resourceGroup().id, storageAccountName, managedIdentity.id, 'Storage Blob Data Contributor')
  scope: storageAccountExisting
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe') // Storage Blob Data Contributor
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
  dependsOn: [
    storage
  ]
}

// ============================================================================
// AZURE COMMUNICATION SERVICES (EMAIL) - Optional
// ============================================================================

module acs 'modules/acs.bicep' = if (deployEmail) {
  name: 'acs-deployment'
  params: {
    location: location
    communicationServiceName: communicationServiceName
    tags: tags
  }
}

// ============================================================================
// APPLICATION INSIGHTS - Optional
// ============================================================================

module appInsights 'modules/applicationInsights.bicep' = if (deployAppInsights) {
  name: 'appInsights-deployment'
  params: {
    location: location
    appInsightsName: appInsightsName
    workspaceName: logAnalyticsName
    tags: tags
  }
}

// ============================================================================
// CONTAINER APP (EDITOR)
// ============================================================================

module webApp 'modules/webApp.bicep' = {
  name: 'webApp-deployment'
  params: {
    location: location
    webAppName: webAppName
    planName: appServicePlanName
    imageName: dockerImage
    keyVaultUri: keyVault.outputs.keyVaultUri
    #disable-next-line BCP318
    adminEmail: deployEmail ? acs.outputs.senderEmailAddress : adminEmail
    #disable-next-line BCP318
    publisherUrl: deployPublisher ? storage.outputs.primaryWebEndpoint : ''
    managedIdentityId: managedIdentity.id
    skuName: environment == 'prod' ? 'P2v3' : 'P1v3'
    skuTier: 'PremiumV3'
    capacity: max(1, minReplicas)
    deploySlot: deploySlot
    tags: tags
  }
  dependsOn: [
    storageBlobDataContributorRole
    kvDbConnectionString
    kvStorageConnectionString
    kvAcsConnectionString
    kvAppInsightsConnectionString
  ]
}

// ============================================================================
// OUTPUTS
// ============================================================================

@description('SkyCMS Editor URL (wait 1-2 min for deployment)')
output editorUrl string = webApp.outputs.url

@description('Web App hostname')
output editorFqdn string = webApp.outputs.hostName

@description('Database Admin Username')
output databaseAdminUsername string = generatedAdminUsername

@description('Database Admin Password')
@secure()
output databaseAdminPassword string = generatedAdminPassword

@description('Storage Account Name (if deployed)')
output storageAccountName string = deployPublisher ? storageAccountName : 'Not deployed'

@description('Static Website URL (if deployed)')
output staticWebsiteUrl string = deployPublisher ? 'https://${storageAccountName}.z13.web.${az.environment().suffixes.storage}' : 'Not deployed'

@description('Communication Services Name (if deployed)')
output communicationServiceName string = deployEmail ? communicationServiceName : 'Not deployed'

@description('Sender Email Address (if email deployed)')
#disable-next-line BCP318
output senderEmailAddress string = deployEmail ? acs.outputs.senderEmailAddress : 'Not deployed'

@description('Application Insights Name (if deployed)')
output appInsightsName string = deployAppInsights ? appInsightsName : 'Not deployed'

@description('Managed Identity Name')
output managedIdentityName string = managedIdentity.name

@description('Key Vault Name')
output keyVaultName string = keyVault.outputs.keyVaultName

@description('Key Vault URI')
output keyVaultUri string = keyVault.outputs.keyVaultUri

@description('Resource Group Name')
output resourceGroupName string = resourceGroup().name

@description('Next Steps')
output nextSteps string = '''
1. Wait 1-2 minutes for Web App to start
2. Visit the Editor URL above
3. Complete the SkyCMS setup wizard
4. ${deployPublisher ? 'Publisher deployed' : 'Publisher not deployed'}
5. ${deployEmail ? 'Email configured with Azure Communication Services' : 'Email not configured'}
6. ${deployAppInsights ? 'Monitoring enabled with Application Insights' : 'Monitoring not configured'}
'''

@description('Human-friendly deployment summary')
output deploymentSummary string = '''Deployment succeeded. Here are the outputs:

Editor URL: ${webApp.outputs.url}
Web app hostname: ${webApp.outputs.hostName}
Database: Azure SQL (${sqlServerName})
Storage account: ${deployPublisher ? storageAccountName : 'Not deployed'}
Static website URL: ${deployPublisher ? format('https://{0}.z13.web.{1}', storageAccountName, az.environment().suffixes.storage) : 'Not deployed'}
Email service: ${deployEmail ? communicationServiceName : 'Not deployed'}
Sender email: ${deployEmail ? acs.outputs.senderEmailAddress : 'Not configured'}
App Insights: ${deployAppInsights ? appInsightsName : 'Not deployed'}
Managed identity: ${managedIdentity.name}

Next steps:
1) Wait ~1–2 minutes for the web app to warm up.
2) Browse the editor URL above and complete the SkyCMS setup wizard.
${deployEmail ? '3) Email is pre-configured and ready to use.' : ''}
${deployAppInsights ? '4) View telemetry in Azure Portal > Application Insights > ' + appInsightsName : ''}
'''
