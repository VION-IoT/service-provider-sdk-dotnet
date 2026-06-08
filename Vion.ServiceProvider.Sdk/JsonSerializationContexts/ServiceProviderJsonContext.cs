using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Vion.Contracts.Events.CloudToMesh;
using Vion.Contracts.Events.MeshToCloud;
using Vion.Contracts.Events.ServiceProviderToMesh;
using Vion.ServiceProvider.Sdk.Setup;
using ServiceProviderRegistrationAcceptedPayload = Vion.Contracts.Events.MeshToServiceProvider.ServiceProviderRegistrationAcceptedPayload;

// ReSharper disable PartialTypeWithSinglePart this must be a partial class because it is generated

namespace Vion.ServiceProvider.Sdk.JsonSerializationContexts
{
    /// <summary>
    ///     Source generation context for all JSON payloads used in the service provider SDK.
    /// </summary>
    [JsonSerializable(typeof(ServiceProviderDeclarationPayload))]
    [JsonSerializable(typeof(ServiceProviderRegistrationPayload))]
    [JsonSerializable(typeof(ServiceProviderRegistrationAcceptedPayload))]
    [JsonSerializable(typeof(ServiceProviderRegistrationRequestPayload))]
    [JsonSerializable(typeof(ServiceProviderSetupSchemaPayload))]
    [JsonSerializable(typeof(ServiceProviderSetupSelectionPayload))]
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
