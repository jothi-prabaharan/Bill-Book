// Bill Book on Azure: eight internal APIs, a public gateway and an always-on
// costing worker on Container Apps; PostgreSQL Flexible Server with no public
// address; Key Vault, Blob Storage and Service Bus reached by managed identity;
// the four web apps on Static Web Apps.
//
// Resource-group scoped, one group per environment. See README.md beside this
// file for the first deploy and for what each parameter means in practice.
targetScope = 'resourceGroup'

// ============================================================================
// Parameters
// ============================================================================

@description('Region for everything except the Static Web Apps. Central India (Pune) by default: this is an Indian GST product and the books belong to Indian businesses. South India (Chennai) is the alternative. Moving later replaces the database server.')
param location string = 'centralindia'

@description('Static Web Apps are offered in a handful of regions only, and India is not one. This is where their control plane lives; the content itself is served from a global edge regardless.')
param staticWebAppLocation string = 'eastasia'

@description('Short name folded into every resource name.')
@minLength(2)
@maxLength(8)
param prefix string = 'billbook'

@description('Image tag every service is deployed at: the commit SHA. Never "latest" — a mutable tag makes two revisions of one service potentially different builds.')
param imageTag string

@description('Phase switch. false deploys the infrastructure and the migration job only; true also rolls out every app. The deploy workflow runs false, then the job, then true — so no service starts against a schema the job has not finished migrating.')
param deployApps bool = true

@description('PostgreSQL SKU. Burstable B2ms (2 vCPU, 8 GiB) is the cost-efficient start; move to GeneralPurpose Standard_D2ds_v5 when sustained load exhausts burst credits.')
param postgresSkuName string = 'Standard_B2ms'

@allowed(['Burstable', 'GeneralPurpose', 'MemoryOptimized'])
param postgresSkuTier string = 'Burstable'

param postgresAdminLogin string = 'billbookadmin'

@secure()
@minLength(16)
@description('Must contain three of: upper case, lower case, digits, symbols — Azure rejects the server otherwise. Must not contain a semicolon, which would truncate the connection string it is embedded in.')
param postgresAdminPassword string

@secure()
@minLength(32)
@description('JWT signing key shared by every service. Differs per environment: a shared key means a token minted in staging is accepted in production.')
param jwtSigningKey string

@secure()
@minLength(32)
@description('Shared key for service-to-service calls marked [InternalOnly].')
param internalApiKey string

@secure()
@description('AES-256 key for SMTP passwords at rest: 32 random bytes, base64. Master refuses to start with anything else.')
param encryptionKey string

@description('Address the first account is created for, once, while mst.Users is empty. It gets no password; the only way in is the reset flow to that mailbox. Empty creates nothing.')
param bootstrapOwnerEmail string = ''

@description('Extra browser origins allowed to call the gateway, beyond the three Static Web Apps — custom domains, once they exist.')
param extraAllowedOrigins array = []

@description('Minimum replicas for the HTTP services. Zero bills nothing while idle at the cost of a cold start. The costing worker ignores this and is always one.')
@minValue(0)
param minReplicas int = 0

@description('Purge protection on the vault. Cannot be switched off once on, and blocks reusing the vault name for 90 days after a delete — but it is what stops a compromised deployer destroying every secret beyond recovery.')
param enableVaultPurgeProtection bool = true

// ============================================================================
// Names
// ============================================================================

// Globally unique names are derived from the resource group, so one template
// deploys any number of environments without a naming collision and without
// anyone choosing names by hand.
var suffix = uniqueString(resourceGroup().id)

var names = {
  logs: 'log-${prefix}'
  vnet: 'vnet-${prefix}'
  environment: 'cae-${prefix}'
  registry: take('cr${prefix}${suffix}', 50)
  vault: take('kv-${prefix}-${suffix}', 24)
  storage: take('st${prefix}${suffix}', 24)
  serviceBus: 'sb-${prefix}-${suffix}'
  postgres: 'psql-${prefix}-${suffix}'
}

// The eight APIs. The keys are Container App names, so they are also the first
// label of each service's internal host name.
var apiKeys = [
  'master'
  'accounting'
  'inventory'
  'sales'
  'purchase'
  'customer'
  'reporting'
  'printing'
]

// Everything that runs as its own identity. One per workload rather than one
// shared, so a compromise of Reporting does not carry Master's ability to write
// secrets.
var identityKeys = concat(apiKeys, ['gateway', 'costing', 'migrate'])

// The gateway proxies all eight. Printing's routes are api/print-templates (the
// template editor) and api/print/render (a document's own service pushing its
// payload under the user's token); both are in the gateway's appsettings.json.
var gatewayClusters = [
  'master'
  'accounting'
  'inventory'
  'sales'
  'purchase'
  'customer'
  'reporting'
  'printing'
]

// One topic per event type. Only CustomerProvisioned is published today. Deployed
// services cannot create topics — their identity can send, not manage — so a new
// event type is added here or its first publish fails naming this file.
var topics = [
  'CustomerProvisioned'
]

var secretNames = [
  'admin-db-connection'
  'tenant-db-connection'
  'jwt-signing-key'
  'internal-api-key'
  'encryption-key'
]

var webApps = [
  'web'
  'portal'
  'admin'
  'docs'
]

// Built-in role definition ids. Fixed across every tenant.
var roles = {
  acrPull: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
  keyVaultSecretsOfficer: 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
  storageBlobDataContributor: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  serviceBusDataSender: '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
}

// ============================================================================
// Monitoring and network
// ============================================================================

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: names.logs
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource vnet 'Microsoft.Network/virtualNetworks@2023-11-01' = {
  name: names.vnet
  location: location
  properties: {
    addressSpace: { addressPrefixes: ['10.20.0.0/16'] }
    subnets: [
      {
        // The Container Apps environment. /23 is comfortably above the /27 a
        // workload-profiles environment needs, leaving room to scale out.
        name: 'snet-apps'
        properties: {
          addressPrefix: '10.20.0.0/23'
          delegations: [
            {
              name: 'apps'
              properties: { serviceName: 'Microsoft.App/environments' }
            }
          ]
        }
      }
      {
        // PostgreSQL, VNet-integrated. Delegated to the server alone, which is
        // what gives it an address here and none on the internet.
        name: 'snet-db'
        properties: {
          addressPrefix: '10.20.2.0/24'
          delegations: [
            {
              name: 'postgres'
              properties: { serviceName: 'Microsoft.DBforPostgreSQL/flexibleServers' }
            }
          ]
        }
      }
    ]
  }
}

// ============================================================================
// PostgreSQL
// ============================================================================

// A VNet-integrated server resolves through a private zone whose name must end
// in .postgres.database.azure.com. Without the link to the VNet the apps would
// resolve the server's name to nothing.
resource postgresDns 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: '${names.postgres}.private.postgres.database.azure.com'
  location: 'global'
}

resource postgresDnsLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: postgresDns
  name: 'vnet'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: { id: vnet.id }
  }
}

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: names.postgres
  location: location
  sku: {
    name: postgresSkuName
    tier: postgresSkuTier
  }
  properties: {
    version: '16'
    administratorLogin: postgresAdminLogin
    administratorLoginPassword: postgresAdminPassword
    storage: {
      storageSizeGB: 32
      autoGrow: 'Enabled'
    }
    backup: {
      // A week of point-in-time restore. These are customers' books.
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: { mode: 'Disabled' }
    network: {
      delegatedSubnetResourceId: vnet.properties.subnets[1].id
      privateDnsZoneArmResourceId: postgresDns.id
      publicNetworkAccess: 'Disabled'
    }
    authConfig: {
      activeDirectoryAuth: 'Disabled'
      passwordAuth: 'Enabled'
    }
  }
  dependsOn: [postgresDnsLink]
}

// Declared so the migration job finds them rather than racing to create them.
// DatabaseMigrationService would create either if missing — this admin login has
// CREATEDB through azure_pg_admin — but a database created by Bicep has a known
// encoding and collation, and one created by whichever process got there first
// has whatever that process asked for.
resource adminDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgres
  name: 'EP_Admin'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

resource tenantDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgres
  name: 'IN000001'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// Every tenant connection is derived from the admin one: TenantDatabaseResolver
// reads mst.Customers.DatabaseName and swaps it in, keeping everything else. So
// the SSL mode and credentials here reach every shard.
var connectionBase = 'Host=${postgres.properties.fullyQualifiedDomainName};Port=5432;Username=${postgresAdminLogin};Password=${postgresAdminPassword};Ssl Mode=Require'

// ============================================================================
// Key Vault
// ============================================================================

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: names.vault
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    // Access by Azure RBAC, not access policies: one permission model across
    // every resource here, and assignments that show up in the same place.
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    // Omitted rather than false when off: the property cannot be set to false.
    enablePurgeProtection: enableVaultPurgeProtection ? true : null
  }
}

var secretValues = {
  'admin-db-connection': '${connectionBase};Database=${adminDatabase.name}'
  'tenant-db-connection': '${connectionBase};Database=${tenantDatabase.name}'
  'jwt-signing-key': jwtSigningKey
  'internal-api-key': internalApiKey
  'encryption-key': encryptionKey
}

resource secrets 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = [for s in secretNames: {
  parent: vault
  name: s
  properties: {
    value: secretValues[s]
  }
}]

// ============================================================================
// Blob Storage
// ============================================================================

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: names.storage
  location: location
  kind: 'StorageV2'
  sku: { name: 'Standard_ZRS' }
  properties: {
    // Entra ID only. A shared key is a secret that grants everything, and one
    // that is refused cannot leak into being useful — which is also why the
    // application reaches this by account URL and managed identity, never by
    // connection string.
    allowSharedKeyAccess: false
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    // GST certificates and archived invoices. An overwrite or a delete is
    // recoverable for two weeks; without these it is not recoverable at all.
    isVersioningEnabled: true
    deleteRetentionPolicy: {
      enabled: true
      days: 14
    }
    containerDeleteRetentionPolicy: {
      enabled: true
      days: 14
    }
  }
}

resource documents 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'documents'
  properties: { publicAccess: 'None' }
}

// ============================================================================
// Service Bus
// ============================================================================

resource serviceBus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: names.serviceBus
  location: location
  // Standard, because topics need it. Basic has queues only.
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    // Entra ID only, for the same reason as the storage account: no SAS key to
    // leak.
    disableLocalAuth: true
    minimumTlsVersion: '1.2'
  }
}

resource eventTopics 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = [for t in topics: {
  parent: serviceBus
  name: t
  properties: {
    // Drops a second send of the same MessageId within ten minutes — the retry
    // after a timeout where the first attempt had in fact landed. Consumers
    // still dedupe; this narrows how often they have to.
    requiresDuplicateDetection: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    defaultMessageTimeToLive: 'P14D'
  }
}]

// ============================================================================
// Container registry
// ============================================================================

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: names.registry
  location: location
  sku: { name: 'Basic' }
  properties: {
    // No admin user. Apps pull with their own identity and the pipeline pushes
    // with its federated one.
    adminUserEnabled: false
  }
}

// ============================================================================
// Identities and access
// ============================================================================

resource identities 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = [for k in identityKeys: {
  name: 'id-${prefix}-${k}'
  location: location
}]

var identityIndex = {
  master: indexOf(identityKeys, 'master')
  sales: indexOf(identityKeys, 'sales')
}

// Every workload pulls its own image.
resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for (k, i) in identityKeys: {
  scope: registry
  name: guid(registry.id, identities[i].id, roles.acrPull)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.acrPull)
    principalId: identities[i].properties.principalId
    principalType: 'ServicePrincipal'
  }
}]

// Every workload reads the secrets its Key Vault references name.
resource vaultRead 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for (k, i) in identityKeys: {
  scope: vault
  name: guid(vault.id, identities[i].id, roles.keyVaultSecretsUser)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsUser)
    principalId: identities[i].properties.principalId
    principalType: 'ServicePrincipal'
  }
}]

// Master alone writes a secret — the SMTP-password key — through ISecretStore.
// Every other service would be refused a write, which is correct.
resource vaultWrite 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: vault
  name: guid(vault.id, identities[identityIndex.master].id, roles.keyVaultSecretsOfficer)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.keyVaultSecretsOfficer)
    principalId: identities[identityIndex.master].properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// The two services that register IFileStorage. Contributor rather than Owner
// because it can read, write and delete blobs and mint user delegation keys —
// the last is what signs download links — and cannot change who else may.
var blobWriters = [identityIndex.master, identityIndex.sales]

resource blobAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for i in blobWriters: {
  scope: storage
  name: guid(storage.id, identities[i].id, roles.storageBlobDataContributor)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.storageBlobDataContributor)
    principalId: identities[i].properties.principalId
    principalType: 'ServicePrincipal'
  }
}]

// Master is the only service that registers IEventPublisher.
resource busSend 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: serviceBus
  name: guid(serviceBus.id, identities[identityIndex.master].id, roles.serviceBusDataSender)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.serviceBusDataSender)
    principalId: identities[identityIndex.master].properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ============================================================================
// Static Web Apps
// ============================================================================

// Free tier: global edge, managed TLS and custom domains, no cost. None of these
// apps needs Standard's SLA or private endpoints — they are static files that
// call a public gateway.
resource sites 'Microsoft.Web/staticSites@2023-12-01' = [for w in webApps: {
  name: 'swa-${prefix}-${w}'
  location: staticWebAppLocation
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    // Deployed from the pipeline with a token, not linked to a repository: the
    // pipeline builds once and deploys the same artifact it tested.
    stagingEnvironmentPolicy: 'Disabled'
    allowConfigFileUpdates: true
  }
}]

var siteOrigin = {
  web: 'https://${sites[0].properties.defaultHostname}'
  portal: 'https://${sites[1].properties.defaultHostname}'
  admin: 'https://${sites[2].properties.defaultHostname}'
}

// ============================================================================
// Container Apps environment
// ============================================================================

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: names.environment
  location: location
  properties: {
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
    vnetConfiguration: {
      infrastructureSubnetId: vnet.properties.subnets[0].id
      // External, so the gateway can take internet traffic. The APIs opt out
      // one by one with internal ingress, which is the finer-grained switch.
      internal: false
    }
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

// Every service's address is computed from the environment's domain rather than
// read from the apps. Master calls Accounting and Accounting calls Master;
// reading each other's outputs would be a cycle Bicep refuses, and
// <app>.internal.<domain> is the host name an internal-ingress app is given
// regardless. The settings module builds every workload's variables from it.
module settings 'modules/settings.bicep' = {
  name: 'settings'
  params: {
    defaultDomain: environment.properties.defaultDomain
    apiKeys: apiKeys
    gatewayClusters: gatewayClusters
    keyVaultUri: vault.properties.vaultUri
    blobEndpoint: storage.properties.primaryEndpoints.blob
    documentsContainer: documents.name
    serviceBusNamespace: '${serviceBus.name}.servicebus.windows.net'
    webOrigin: siteOrigin.web
    portalOrigin: siteOrigin.portal
    allowedOrigins: concat([siteOrigin.web, siteOrigin.portal, siteOrigin.admin], extraAllowedOrigins)
    bootstrapOwnerEmail: bootstrapOwnerEmail
  }
}

var image = '${registry.properties.loginServer}/{0}:${imageTag}'

// ============================================================================
// Migration job — deployed in both phases
// ============================================================================

// Runs Master's image with Migrations:ExitWhenDone, which migrates every schema
// and then stops with exit code 0. A failed migration exits non-zero, the job
// fails, and the pipeline stops before any new revision serves traffic.
//
// Every service also migrates its own schema on startup. This job exists so
// that happens once, first, rather than eight services racing at rollout.
resource migrate 'Microsoft.App/jobs@2024-03-01' = {
  name: 'migrate'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identities[indexOf(identityKeys, 'migrate')].id}': {} }
  }
  properties: {
    environmentId: environment.id
    workloadProfileName: 'Consumption'
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 1800
      // A migration that fails is looked at, not retried into a half-applied
      // schema.
      replicaRetryLimit: 0
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: identities[indexOf(identityKeys, 'migrate')].id
        }
      ]
      secrets: [for s in secretNames: {
        name: s
        keyVaultUrl: '${vault.properties.vaultUri}secrets/${s}'
        identity: identities[indexOf(identityKeys, 'migrate')].id
      }]
    }
    template: {
      containers: [
        {
          name: 'migrate'
          image: format(image, 'master')
          resources: {
            cpu: json('1')
            memory: '2Gi'
          }
          env: concat([
            { name: 'AZURE_CLIENT_ID', value: identities[indexOf(identityKeys, 'migrate')].properties.clientId }
            { name: 'Migrations__ExitWhenDone', value: 'true' }
          ], settings.outputs.masterEnv)
        }
      ]
    }
  }
  // The job reads the vault and pulls from the registry the moment it runs.
  dependsOn: [acrPull, vaultRead, secrets]
}

// ============================================================================
// Apps — phase two only
// ============================================================================

module apis 'modules/app.bicep' = [for (k, i) in apiKeys: if (deployApps) {
  name: 'app-${k}'
  params: {
    name: k
    location: location
    environmentId: environment.id
    identityId: identities[indexOf(identityKeys, k)].id
    identityClientId: identities[indexOf(identityKeys, k)].properties.clientId
    registryServer: registry.properties.loginServer
    image: format(image, k)
    ingress: 'internal'
    minReplicas: minReplicas
    env: k == 'master' ? settings.outputs.masterEnv : settings.outputs.commonEnv
    keyVaultSecrets: secretNames
    keyVaultUri: vault.properties.vaultUri
  }
  dependsOn: [acrPull, vaultRead, vaultWrite, blobAccess, busSend, secrets, migrate]
}]

module gateway 'modules/app.bicep' = if (deployApps) {
  name: 'app-gateway'
  params: {
    name: 'gateway'
    location: location
    environmentId: environment.id
    identityId: identities[indexOf(identityKeys, 'gateway')].id
    identityClientId: identities[indexOf(identityKeys, 'gateway')].properties.clientId
    registryServer: registry.properties.loginServer
    image: format(image, 'gateway')
    ingress: 'external'
    minReplicas: minReplicas
    env: settings.outputs.gatewayEnv
    keyVaultSecrets: secretNames
    keyVaultUri: vault.properties.vaultUri
    healthPath: '/health'
    cpu: '0.25'
    memory: '0.5Gi'
  }
  dependsOn: [acrPull, vaultRead, secrets, migrate]
}

// Not a request-driven service. inv.StockMovements is the queue: the worker
// claims a movement with a guarded Pending -> InProgress update and costs it in
// order. One replica, always — zero would stop claiming the moment traffic
// stopped, and a second only adds contention on the same rows, because the
// guarded claim means throughput is not the constraint.
module costing 'modules/app.bicep' = if (deployApps) {
  name: 'app-costing'
  params: {
    name: 'costing-worker'
    location: location
    environmentId: environment.id
    identityId: identities[indexOf(identityKeys, 'costing')].id
    identityClientId: identities[indexOf(identityKeys, 'costing')].properties.clientId
    registryServer: registry.properties.loginServer
    image: format(image, 'costing-worker')
    ingress: 'none'
    minReplicas: 1
    maxReplicas: 1
    env: settings.outputs.commonEnv
    keyVaultSecrets: secretNames
    keyVaultUri: vault.properties.vaultUri
  }
  dependsOn: [acrPull, vaultRead, secrets, migrate]
}

// ============================================================================
// Outputs
// ============================================================================

@description('The public API entry point. External ingress host names are <app>.<defaultDomain>.')
output gatewayUrl string = 'https://gateway.${environment.properties.defaultDomain}'

output registryLoginServer string = registry.properties.loginServer
output registryName string = registry.name
output migrateJobName string = migrate.name

@description('Static Web App names and origins, in the order web, portal, admin, docs.')
output sites array = [for (w, i) in webApps: {
  app: w
  name: sites[i].name
  origin: 'https://${sites[i].properties.defaultHostname}'
}]

output postgresServer string = postgres.properties.fullyQualifiedDomainName
output keyVaultName string = vault.name
