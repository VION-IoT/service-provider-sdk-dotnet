# MQTT Topics Documentation

This document provides a comprehensive overview of all MQTT topics used in the Service Provider SDK.

## MQTT Clients

The SDK implements MQTT communication through:

1. **`ServiceProviderClient`** (`Vion.ServiceProvider.Sdk\RegistrationFlow\ServiceProviderClient.cs`) - Uses:
   - `IMqttClient _operationalClient` - For operational communication after registration
   - Temporary registration client - Created during registration flow and disposed after completion

---

## Subscribed Topics

| Topic Pattern | Phase | File | Method/Line | Description |
|---------------|-------|------|------|--------|
| `{Topics.ServiceProviderRegistrationAccepted}/{secret}` | Registration | `ServiceProviderClient.cs` | `RegisterAsync` (~705) | Accepts registration with operational credentials |
| `{Topics.ServiceProviderRegistrationDenied}/{secret}` | Registration | `ServiceProviderClient.cs` | `RegisterAsync` (~706) | Denies registration request |
| `{installationTopic}{serviceProviderIdentifier}/serviceProvider/setup/selection` | Setup | `ServiceProviderClient.cs` | `SendSetupSchemaAsync` (~525) | Receives setup selection from mesh |
| `{installationTopic}{serviceProviderIdentifier}/{service}/{contract}/#` | Operational | `ServiceProviderClientConfigurationBuilder.cs` | `WithContractHandler` (~335) | Contract-specific message handlers |
| Custom topics registered via `WithHandler` | Operational | `ServiceProviderClientConfigurationBuilder.cs` | `WithHandler` (~257) | User-defined message handlers |

---

## Published Topics

| Topic Pattern | Phase | File | Method/Line | Description |
|---------------|-------|------|------|--------|
| `{Topics.ServiceProviderRegistrationRequest}/{secret}` | Registration | `ServiceProviderClient.cs` | `RegisterAsync` (~758) | Requests registration with mesh broker |
| `{installationTopic}{serviceProviderIdentifier}/serviceProvider/setup/schema` | Setup | `ServiceProviderClient.cs` | `SendSetupSchemaAsync` (~590) | Publishes setup schema for configuration |
| `{installationTopic}/{serviceProviderIdentifier}{Topics.ServiceProviderDeclaration}` | Operational | `ServiceProviderClient.cs` | `SendDeclarationAsync` (~893) | Declares service provider capabilities |
| `{installationTopic}{serviceProviderIdentifier}{Topics.ComponentHealthState}` | Operational | `ServiceProviderClient.cs` | `ConnectOperationalClientAsync` (~400), Last Will (~401) | Health status publication and last will message |
| Response topic from request | Operational | `ServiceProviderClient.cs` | Health handler (~303-315) | Health status responses to requests |
| Custom topics via `PublishAsync` | Operational | `ServiceProviderClient.cs` | `PublishAsync` (~158) | User-defined publications |

---

## Topic Construction Helpers

The SDK provides helper methods in `ServiceProviderTopics` class for building topic strings:

| Method | Returns | Usage |
|--------|---------|-------|
| `GetRegistrationAcceptedTopic(secret)` | `{Topics.ServiceProviderRegistrationAccepted}/{secret}` | Registration acceptance subscription |
| `GetRegistrationDeniedTopic(secret)` | `{Topics.ServiceProviderRegistrationDenied}/{secret}` | Registration denial subscription |
| `GetSetupSchemaTopic(installationTopic, serviceProviderIdentifier)` | `{installationTopic}{serviceProviderIdentifier}/serviceProvider/setup/schema` | Setup schema publication |
| `GetSelectionTopic(installationTopic, serviceProviderIdentifier)` | `{installationTopic}{serviceProviderIdentifier}/serviceProvider/setup/selection` | Setup selection subscription |
| `GetContractTopicFilter(installationTopic, serviceProviderIdentifier, serviceAndContractIdentifierPart)` | `{installationTopic}{serviceProviderIdentifier}/{serviceAndContractIdentifierPart}/#` | Contract handler subscriptions |

---

## Topic Constants

The following constants are defined in the external `Shared.Contracts.Mqtt.Topics` assembly:

- `Topics.ServiceProviderRegistrationRequest`
- `Topics.ServiceProviderRegistrationAccepted`
- `Topics.ServiceProviderRegistrationDenied`
- `Topics.ServiceProviderDeclaration`
- `Topics.ComponentHealthGet`
- `Topics.ComponentHealthState`

> **Note:** The actual string values for these constants are defined in the external `Shared.Contracts.Mqtt` assembly/package.

---

## Dynamic Topic Parts

| Variable | Source | Description |
|----------|--------|-------------|
| `{secret}` | Configuration parameter | Authentication secret provided during client configuration |
| `{installationTopic}` | Registration response | Received from `ServiceProviderRegistrationAcceptedPayload.InstallationTopic` |
| `{serviceProviderIdentifier}` | Configuration parameter | Provided in `MqttConnectionData.ServiceProviderIdentifier` |
| `{service}/{contract}` | Handler configuration | Defined when registering contract handlers via `WithContractHandler` |

---

## Topic Phases

### Registration Phase
Topics used during initial service provider registration with the mesh:
- Subscribe to acceptance/denial topics with secret
- Publish registration request
- Receive operational MQTT credentials

### Setup Phase (Optional)
Topics used for service provider configuration schema and selection:
- Publish setup schema
- Subscribe to setup selection
- Wait for configuration from mesh

### Operational Phase
Topics used for normal operation after successful registration:
- Publish service provider declaration
- Publish health status (with Last Will)
- Subscribe to contract-specific topics
- Handle incoming messages via registered handlers
