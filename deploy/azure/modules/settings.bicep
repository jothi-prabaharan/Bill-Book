// The environment variables each workload receives, built in one place.
//
// A module rather than variables in main.bicep for a mechanical reason worth
// knowing: the service URLs depend on the Container Apps environment's domain,
// which exists only once the environment does, and Bicep will not loop over a
// value like that in a variable. Passed in here as a parameter it is known when
// this module starts, so the loops below are allowed — and the lists come back
// as outputs the apps and the migration job consume.

param defaultDomain string
param apiKeys array
param gatewayClusters array
param keyVaultUri string
param blobEndpoint string
param documentsContainer string
param serviceBusNamespace string
param webOrigin string
param portalOrigin string
param allowedOrigins array
param bootstrapOwnerEmail string
param bootstrapOperatorEmails string

var serviceUrl = toObject(apiKeys, k => k, k => 'https://${k}.internal.${defaultDomain}')

// Every inter-service URL on every service, deliberately. Each service's
// RequiredSetting list differs and several ship localhost defaults in
// appsettings.json — which in production is worse than missing, because it
// fails by calling nothing rather than by refusing to start. Setting all of
// them everywhere removes the class of mistake instead of auditing for it.
var baseUrlEnv = [for k in apiKeys: {
  name: '${toUpper(substring(k, 0, 1))}${substring(k, 1)}__BaseUrl'
  value: serviceUrl[k]
}]

var common = concat([
  { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
  { name: 'KeyVault__Uri', value: keyVaultUri }
  { name: 'Storage__AccountUrl', value: blobEndpoint }
  { name: 'Storage__Container', value: documentsContainer }
  { name: 'ServiceBus__Namespace', value: serviceBusNamespace }
  { name: 'ConnectionStrings__AdminDatabase', secretRef: 'admin-db-connection' }
  { name: 'ConnectionStrings__TenantDatabase', secretRef: 'tenant-db-connection' }
  { name: 'Jwt__SigningKey', secretRef: 'jwt-signing-key' }
  { name: 'Internal__ApiKey', secretRef: 'internal-api-key' }
  { name: 'Encryption__Key', secretRef: 'encryption-key' }
], baseUrlEnv)

// Master's own: where it seeds a new branch, where invitation and portal links
// point, and the first account.
var master = concat(common, [
  { name: 'Seeding__Accounting', value: serviceUrl.accounting }
  { name: 'Seeding__Inventory', value: serviceUrl.inventory }
  { name: 'Seeding__Sales', value: serviceUrl.sales }
  { name: 'Seeding__Purchase', value: serviceUrl.purchase }
  { name: 'Seeding__Reporting', value: serviceUrl.reporting }
  { name: 'Seeding__Printing', value: serviceUrl.printing }
  { name: 'App__BaseUrl', value: webOrigin }
  { name: 'Portal__BaseUrl', value: portalOrigin }
  { name: 'Bootstrap__OwnerEmail', value: bootstrapOwnerEmail }
  { name: 'Bootstrap__OperatorEmails', value: bootstrapOperatorEmails }
])

// Overrides the committed localhost destinations. A Container Apps host name is
// not knowable until the environment exists, so it is set here rather than in
// appsettings.Production.json.
var clusterEnv = [for c in gatewayClusters: {
  name: 'ReverseProxy__Clusters__${c}__Destinations__d1__Address'
  value: '${serviceUrl[c]}/'
}]

var gateway = concat(common, [
  { name: 'Cors__AllowedOrigins', value: join(allowedOrigins, ',') }
], clusterEnv)

output commonEnv array = common
output masterEnv array = master
output gatewayEnv array = gateway
