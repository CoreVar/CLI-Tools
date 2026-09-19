@description('Globally unique DNS label for the registry')
param dnsName string
@secure()
param publishKey string
param tenant string = 'demo'
param location string = resourceGroup().location
param image string = 'ghcr.io/corevar/cli-registry:latest'

var storageName = take(replace('cvcli${uniqueString(resourceGroup().id)}', '-', ''), 24)
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
}
resource files 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = { parent: storage name: 'default' }
resource share 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: files
  name: 'registry'
  properties: { shareQuota: 10 }
}
resource registry 'Microsoft.ContainerInstance/containerGroups@2023-05-01' = {
  name: 'corevar-cli-registry'
  location: location
  properties: {
    osType: 'Linux'
    restartPolicy: 'Always'
    ipAddress: { type: 'Public', dnsNameLabel: dnsName, ports: [{ protocol: 'TCP', port: 8080 }] }
    containers: [{
      name: 'registry'
      properties: {
        image: image
        ports: [{ port: 8080 }]
        resources: { requests: { cpu: 1, memoryInGB: 1 } }
        environmentVariables: [
          { name: 'COREVAR_REGISTRY_DATA', value: '/data' }
          { name: 'COREVAR_REGISTRY_API_KEYS', secureValue: '${tenant}=${publishKey}' }
        ]
        volumeMounts: [{ name: 'data', mountPath: '/data' }]
      }
    }]
    volumes: [{
      name: 'data'
      azureFile: {
        shareName: share.name
        storageAccountName: storage.name
        storageAccountKey: storage.listKeys().keys[0].value
      }
    }]
  }
}
output endpoint string = 'http://${registry.properties.ipAddress.fqdn}:8080/'
