using Microsoft.Extensions.Logging;
using Shared.Contracts.Events.CloudToMesh;
using Shared.Contracts.Events.MeshToCloud;
using Shared.Contracts.Events.ServiceProviderToMesh;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Vion.ServiceProvider.Sdk.RegistrationFlow;

// ReSharper disable PartialTypeWithSinglePart this must be a partial class because it is generated

namespace Vion.ServiceProvider.Sdk.JsonSerializationContexts
{
    [JsonSerializable(typeof(ServiceProviderDeclarationPayload))]
    [JsonSerializable(typeof(ServiceProviderRegistrationPayload))]
    [JsonSerializable(typeof(Shared.Contracts.Events.MeshToServiceProvider.ServiceProviderRegistrationAcceptedPayload))]
    [JsonSerializable(typeof(ServiceProviderRegistrationRequestPayload))]
    [JsonSerializable(typeof(Payloads.ServiceProviderSetupSchemaPayload))]
    [JsonSerializable(typeof(Payloads.ServiceProviderSetupSelectionPayload))]
    [JsonSerializable(typeof(SetLogLevelPayload))]
    [JsonSerializable(typeof(LogLevelStatePayload))]
    [JsonSerializable(typeof(Dictionary<string, object>))]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(bool))]
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
                                 DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
                                 Converters = [typeof(JsonStringEnumConverter<LogLevel>)])]
    public partial class ServiceProviderJsonContext : JsonSerializerContext;
}
