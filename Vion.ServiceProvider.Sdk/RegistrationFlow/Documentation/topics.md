# MQTT Topics Documentation

This document provides a comprehensive overview of all MQTT topics used in the Service Provider SDK.

## MQTT Clients

The SDK implements MQTT communication through:

1. **`ServiceProviderClient`** (`Vion.ServiceProvider.Sdk\RegistrationFlow\ServiceProviderClient.cs`) - Uses:
    - `IMqttClient _operationalClient` - For operational communication after registration
    - Temporary registration client - Created during registration flow and disposed after completion

---

## Subscribed Topics

| Topic Pattern                                                                    | Phase        | File                                           | Method                 | Description                                       |
|----------------------------------------------------------------------------------|--------------|------------------------------------------------|------------------------|---------------------------------------------------|
| `{Topics.ServiceProviderRegistrationAccepted}/{registrationClientId}`              | Registration | `ServiceProviderClient.cs`                     | `RegisterAsync`        | Accepts registration with operational credentials |
| `{Topics.ServiceProviderRegistrationDenied}/{registrationClientId}`                | Registration | `ServiceProviderClient.cs`                     | `RegisterAsync`        | Denies registration request                       |
| `{installationTopic}{serviceProviderIdentifier}/serviceProvider/setup/selection` | Setup        | `ServiceProviderClient.cs`                     | `SendSetupSchemaAsync` | Receives the setup selection                      |
| `{installationTopic}{serviceProviderIdentifier}/{service}/{contract}/#`          | Operational  | `ServiceProviderClientConfigurationBuilder.cs` | `WithContractHandler`  | Contract-specific message handlers                |
| Custom topics registered via `WithHandler`                                       | Operational  | `ServiceProviderClientConfigurationBuilder.cs` | `WithHandler`          | User-defined message handlers                     |

> **Every inbound message on the operational connection must carry MQTT v5 correlation data**, as a 16-byte GUID or its 36-character string form. The check runs before topic matching, so a
> message without it is logged at `Error` and dropped with no handler run and no reply — including a subscription that is not request-shaped. See
> [Required message elements](RegistrationFlowStateMachine.md#required-message-elements).

---

## Published Topics

| Topic Pattern                                                                        | Phase        | File                       | Method                                         | Description                                     |
|--------------------------------------------------------------------------------------|--------------|----------------------------|------------------------------------------------|-------------------------------------------------|
| `{Topics.ServiceProviderRegistrationRequest}/{registrationClientId}`                   | Registration | `ServiceProviderClient.cs` | `RegisterAsync`                                | Requests registration with the broker           |
| `{installationTopic}{serviceProviderIdentifier}/serviceProvider/setup/schema`        | Setup        | `ServiceProviderClient.cs` | `SendSetupSchemaAsync`                         | Publishes setup schema for configuration        |
| `{installationTopic}/{serviceProviderIdentifier}{Topics.ServiceProviderDeclaration}` | Operational  | `ServiceProviderClient.cs` | `SendDeclarationAsync`                         | Declares service provider capabilities          |
| `{installationTopic}{serviceProviderIdentifier}{Topics.ComponentHealthState}`        | Operational  | `ServiceProviderClient.cs` | `ConnectOperationalClientAsync` + Last Will    | Health status publication and last will message |
| Response topic from request                                                          | Operational  | `ServiceProviderClient.cs` | Health handler (`RegisterAdditionalHandlers`)  | Health status responses to requests             |
| Custom topics via `PublishMessageAsync` / `PublishResponseAsync`                     | Operational  | `ServiceProviderClient.cs` | `PublishMessageAsync` / `PublishResponseAsync` | User-defined publications                       |

---

## Topic Construction Helpers

The SDK provides helper methods in `ServiceProviderTopics` class for building topic strings:

| Method                                                                                                   | Returns                                                                               | Usage                                |
|----------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------|--------------------------------------|
| `GetRegistrationRequestTopic(registrationClientId)`                                                      | `{Topics.ServiceProviderRegistrationRequest}/{registrationClientId}`                  | Registration request publication     |
| `GetRegistrationAcceptedTopic(registrationClientId)`                                                     | `{Topics.ServiceProviderRegistrationAccepted}/{registrationClientId}`                 | Registration acceptance subscription |
| `GetRegistrationDeniedTopic(registrationClientId)`                                                       | `{Topics.ServiceProviderRegistrationDenied}/{registrationClientId}`                   | Registration denial subscription     |
| `GetSetupSchemaTopic(installationTopic, serviceProviderIdentifier)`                                      | `{installationTopic}{serviceProviderIdentifier}/serviceProvider/setup/schema`         | Setup schema publication             |
| `GetSelectionTopic(installationTopic, serviceProviderIdentifier)`                                        | `{installationTopic}{serviceProviderIdentifier}/serviceProvider/setup/selection`      | Setup selection subscription         |
| `GetContractTopicFilter(installationTopic, serviceProviderIdentifier, serviceAndContractIdentifierPart)` | `{installationTopic}{serviceProviderIdentifier}/{serviceAndContractIdentifierPart}/#` | Contract handler subscriptions       |

---

## Topic Constants

The following constants are defined in the external `Vion.Contracts.Mqtt.Topics` class:

- `Topics.ServiceProviderRegistrationRequest`
- `Topics.ServiceProviderRegistrationAccepted`
- `Topics.ServiceProviderRegistrationDenied`
- `Topics.ServiceProviderDeclaration`
- `Topics.ComponentHealthGet`
- `Topics.ComponentHealthState`

> **Note:** The actual string values for these constants are defined in the external `Vion.Contracts.Mqtt` assembly/package.

---

## Dynamic Topic Parts

| Variable                      | Source                  | Description                                                                  |
|-------------------------------|-------------------------|------------------------------------------------------------------------------|
| `{registrationClientId}`      | Generated per attempt   | Random GUID, also the registration connection's MQTT client-id (see below)   |
| `{installationTopic}`         | Registration response   | Received from `ServiceProviderRegistrationAcceptedPayload.InstallationTopic` |
| `{serviceProviderIdentifier}` | Configuration parameter | Provided in `MqttConnectionData.ServiceProviderIdentifier`                   |
| `{service}/{contract}`        | Handler configuration   | Defined when registering contract handlers via `WithContractHandler`         |

---

## Topic Phases

### Registration Phase

Topics used during initial service provider registration:

All three registration topics are keyed on the **registration client-id** — a random GUID the SDK generates per registration
attempt, which is also the MQTT client-id of the registration connection. The secret travels in the request *payload*, not the
topic, because topics are logged far more readily than payloads. Nothing here is retained in either direction.

- Subscribe to the acceptance/denial topics for this attempt's client-id — before publishing, since a response that arrives
  first is gone for good
- Publish the registration request (`ServiceProviderIdentifier` + `Secret` in the payload), repeated on
  `RegistrationRepublishInterval` until accepted
- Receive operational MQTT credentials

### Setup Phase (Optional)

Topics used for service provider configuration schema and selection:

- Publish setup schema
- Subscribe to setup selection
- Wait for the configuration selection

### Operational Phase

Topics used for normal operation after successful registration:

- Publish service provider declaration
- Publish health status (with Last Will)
- Subscribe to contract-specific topics
- Handle incoming messages via registered handlers
