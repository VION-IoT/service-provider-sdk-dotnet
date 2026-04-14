using System.Collections.Generic;
using System.Text.Json.Serialization;
using Shared.Contracts.Events.MeshToCloud;
using Shared.Contracts.Events.MeshToServiceProvider;
using Shared.Contracts.Events.ServiceProviderToMesh;
using Vion.ServiceProvider.Sdk.RegistrationFlow;

// ReSharper disable PartialTypeWithSinglePart this must be a partial class because it is generated

namespace Vion.ServiceProvider.Sdk.JsonSerializationContexts
{
    [JsonSerializable(typeof(ServiceProviderDeclarationPayload))]
    [JsonSerializable(typeof(ServiceProviderRegistrationPayload))]
    [JsonSerializable(typeof(ServiceProviderRegistrationAcceptedPayload))]
    [JsonSerializable(typeof(ServiceProviderRegistrationRequestPayload))]
    [JsonSerializable(typeof(Payloads.ServiceProviderSetupSchemaPayload))]
    [JsonSerializable(typeof(Payloads.ServiceProviderSetupSelectionPayload))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(bool))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase)]
    public partial class ServiceProviderJsonContext : JsonSerializerContext;
}