// One Container App. Used for the eight APIs, the gateway and the costing worker,
// which differ only in the parameters below — so the parts they must agree on
// (the identity wiring, the registry, the Key Vault references, the port) are
// written once here rather than ten times in main.bicep.

@description('Container App name. Also the first label of its internal host name, which is how every other service reaches it.')
param name string

param location string
param environmentId string

@description('Resource id of the user-assigned identity the app runs as.')
param identityId string

@description('Client id of that identity. Exported as AZURE_CLIENT_ID, without which DefaultAzureCredential looks for a system-assigned identity and fails at the first secret read.')
param identityClientId string

param registryServer string
param image string

@allowed(['none', 'internal', 'external'])
@description('none: no HTTP at all (the costing worker). internal: reachable only inside the environment. external: the internet.')
param ingress string

param minReplicas int = 0
param maxReplicas int = 10

@description('Plain {name, value} and {name, secretRef} pairs, appended to the ones every app gets.')
param env array = []

@description('Key Vault secret names to mount as Container App secrets, each readable by env entries with secretRef.')
param keyVaultSecrets array = []

@description('The vault URI, with its trailing slash.')
param keyVaultUri string

@description('Path for an HTTP readiness probe, or empty for none. Only the gateway has a health route; the APIs are probed on TCP.')
param healthPath string = ''

param cpu string = '0.5'
param memory string = '1Gi'

var hasHttp = ingress != 'none'

// TCP startup probe for anything serving HTTP: EF Core builds its model and every
// service runs its own migration check before Kestrel binds, which on a cold
// replica is not instant. Ten attempts ten seconds apart, after a ten second
// grace, is about two minutes — generous, because a probe that gives up on a
// healthy service turns a slow start into a restart loop.
var startupProbe = {
  type: 'Startup'
  tcpSocket: { port: 8080 }
  initialDelaySeconds: 10
  periodSeconds: 10
  failureThreshold: 10
}

var livenessProbe = {
  type: 'Liveness'
  tcpSocket: { port: 8080 }
  periodSeconds: 30
  failureThreshold: 3
}

var readinessProbe = {
  type: 'Readiness'
  httpGet: { path: healthPath, port: 8080 }
  periodSeconds: 15
  failureThreshold: 3
}

var probes = !hasHttp ? [] : empty(healthPath) ? [startupProbe, livenessProbe] : [startupProbe, livenessProbe, readinessProbe]

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identityId}': {} }
  }
  properties: {
    environmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      // One revision live at a time. A deploy replaces the running revision
      // rather than splitting traffic, which is what a migration-first rollout
      // needs: two revisions against one schema is the thing it exists to avoid.
      activeRevisionsMode: 'Single'
      ingress: hasHttp ? {
        external: ingress == 'external'
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      } : null
      // Pulled with the app's own identity. That is why these are user-assigned
      // rather than system-assigned: a system identity does not exist until the
      // app does, so it could not hold AcrPull in time for the app's first pull.
      registries: [
        {
          server: registryServer
          identity: identityId
        }
      ]
      // Versionless Key Vault references. Container Apps re-reads them
      // periodically, so rotating a secret in the vault reaches running
      // replicas without a redeploy.
      secrets: [for s in keyVaultSecrets: {
        name: s
        keyVaultUrl: '${keyVaultUri}secrets/${s}'
        identity: identityId
      }]
    }
    template: {
      containers: [
        {
          name: name
          image: image
          resources: {
            cpu: json(cpu)
            memory: memory
          }
          env: concat([
            { name: 'AZURE_CLIENT_ID', value: identityClientId }
          ], env)
          probes: probes
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
}

output fqdn string = hasHttp ? app.properties.configuration.ingress.fqdn : ''
