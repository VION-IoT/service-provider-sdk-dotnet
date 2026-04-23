Now let me find the `Topics` class to see where the base topic strings are defined, and also search for any other MQTT clients:
Now let me find the `Topics` class where the constants are defined:
The `Topics` class comes from `Vion.Contracts.Mqtt`. This is likely a referenced NuGet package or shared library. Let me search for where the IServiceProviderPublish is used and check for any other topic references:
Now I have a comprehensive view of both MQTT clients and how topics are used. Based on my analysis, here's a complete summary of all MQTT topics:

---

## MQTT Topics Summary

### **Two MQTT Clients Found:**
1. **`MqttBridge`** (`HalSim\MqttBridge.cs`) - Uses a single `IMqttClient _client`
2. **`ServiceProviderClient`** (`HalSim\RegistrationFlow\ServiceProviderClient.cs`) - Uses `IMqttClient _operationalClient` and a temporary registration client

---

### **Subscribed Topics**

| Topic Pattern | Phase | File | Line | Origin |
|---------------|-------|------|------|--------|
| `{Topics.ServiceProviderRegistrationAccepted}/{_secret}` | Registration | `MqttBridge.cs` | 68-69 | Constructor (constant + secret) |
| `{Topics.ServiceProviderRegistrationDenied}/{_secret}` | Registration | `MqttBridge.cs` | 68-69 | Constructor (constant + secret) |
| `{_installationTopic}/{_serviceProviderIdentifier}/+/+/hw/do/set` | Operational | `MqttBridge.cs` | 141 | Set after registration accepted |
| `{_installationTopic}/{_serviceProviderIdentifier}/+/+/hw/ao/set` | Operational | `MqttBridge.cs` | 142 | Set after registration accepted |
| `{_installationTopic}/{_serviceProviderIdentifier}{Topics.ComponentHealthGet}` | Operational | `MqttBridge.cs` | 143 | Set after registration accepted |
| `{Topics.ServiceProviderRegistrationAccepted}/{secret}` | Registration | `ServiceProviderClient.cs` | 377 | `RegisterAsync` |
| `{Topics.ServiceProviderRegistrationDenied}/{secret}` | Registration | `ServiceProviderClient.cs` | 378 | `RegisterAsync` |
| `{_operationalData.InstallationTopic}{Topics.ComponentHealthGet}` | Operational | `ServiceProviderClient.cs` | 205 | `SetupHandlersAsync` |
| `{installationTopic}/{serviceProviderIdentifier}/{service}/{contract}/#` | Operational | `ServiceProviderClientConfigurationBuilder.cs` | 183 | Contract handlers (via builder) |

---

### **Published Topics**

| Topic Pattern | Phase | File | Line | Origin |
|---------------|-------|------|------|--------|
| `{Topics.ServiceProviderRegistrationRequest}/{_secret}` | Registration | `MqttBridge.cs` | 291 | `PublishRegistrationAsync` |
| `{_installationTopic}/{_serviceProviderIdentifier}/{service}/{contract}/hw/{type}/state` | Operational | `MqttBridge.cs` | 96 | `PublishAsync` (DI/AI/DO/AO state) |
| `{_installationTopic}/{_serviceProviderIdentifier}{Topics.ServiceProviderDeclaration}` | Operational | `MqttBridge.cs` | 318 | `PublishDeclarationAsync` |
| `{_topicComponentHealthState}` = `{_installationTopic}/{_serviceProviderIdentifier}{Topics.ComponentHealthState}` | Operational | `MqttBridge.cs` | 144, 367 | Health publish + Last Will |
| Response topic from received message (`e.ApplicationMessage.ResponseTopic`) | Operational | `MqttBridge.cs` | 159 | Health response |
| `{Topics.ServiceProviderRegistrationRequest}/{serviceProviderIdentifier}/{secret}` | Registration | `ServiceProviderClient.cs` | 422 | `RegisterAsync` |
| `{installationTopic}{Topics.ServiceProviderDeclaration}/{serviceProviderIdentifier}` | Operational | `ServiceProviderClient.cs` | 492 | `SendDeclarationAsync` |
| `{_operationalData.InstallationTopic}{Topics.ComponentHealthState}/{serviceProviderIdentifier}` | Operational | `ServiceProviderClient.cs` | 287 | Last Will + health publish |
| Response topic from received message (health response) | Operational | `ServiceProviderClient.cs` | 209-217 | Health handler |

---

### **Topic Constants (from `Vion.Contracts.Mqtt.Topics`)**

These are external constants referenced throughout the code:
- `Topics.ServiceProviderRegistrationRequest`
- `Topics.ServiceProviderRegistrationAccepted`
- `Topics.ServiceProviderRegistrationDenied`
- `Topics.ServiceProviderDeclaration`
- `Topics.ComponentHealthGet`
- `Topics.ComponentHealthState`

> **Note:** The actual string values for these constants are defined in the external `Vion.Contracts.Mqtt` assembly/package and are not available in this codebase.

---

### **Dynamic Topic Parts**

| Variable | Source |
|----------|--------|
| `{_secret}` | Generated GUID stored in `data/secret.txt` (`MqttBridge.cs:147`) |
| `{_installationTopic}` | From registration accepted payload (`acceptedPayload.InstallationTopic`) |
| `{_serviceProviderIdentifier}` | From config or environment variable `SERVICE_PROVIDER_IDENTIFIER` |
| `{service}/{contract}` | From `ServiceProviderDefinition.GetServiceContract()` |
| `{type}` | One of: `di`, `ai`, `do`, `ao` |