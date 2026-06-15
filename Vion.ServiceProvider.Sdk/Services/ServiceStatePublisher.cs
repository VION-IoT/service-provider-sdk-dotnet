using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet.Protocol;
using Vion.Contracts.Conventions;
using Vion.Contracts.Events.ServiceProviderToMesh;
using Vion.Contracts.Mqtt;
using Vion.ServiceProvider.Sdk.JsonSerializationContexts;
using Vion.ServiceProvider.Sdk.RegistrationFlow;

namespace Vion.ServiceProvider.Sdk.Services
{
    /// <summary>
    ///     Publishes service-field values as retained JSON state messages on the field's state topic.
    /// </summary>
    public sealed class ServiceStatePublisher : IServiceStatePublisher
    {
        private readonly ConcurrentDictionary<TopicKey, string> _topicByField = new();

        /// <inheritdoc />
        public Task PublishFieldAsync(IServiceProviderPublisher publisher, string serviceIdentifier, IServiceField field, JsonNode? value, CancellationToken cancellationToken)
        {
            var publishedValue = field.IsWriteOnly && value is not null ? JsonValue.Create(WriteOnlyConventions.RedactedSentinel) : value;
            var (payload, schema) = field.Kind switch
            {
                ServiceFieldKind.Property => (JsonSerializer.SerializeToUtf8Bytes(new PropertyStatePayload(publishedValue),
                                                                                  ServiceProviderJsonContext.Default.PropertyStatePayload), nameof(PropertyStatePayload)),
                ServiceFieldKind.MeasuringPoint => (JsonSerializer.SerializeToUtf8Bytes(new MeasuringPointStatePayload(publishedValue),
                                                                                        ServiceProviderJsonContext.Default.MeasuringPointStatePayload),
                                                       nameof(MeasuringPointStatePayload)),
                _ => throw new InvalidOperationException($"Unknown field kind '{field.Kind}' on field '{field.Name}'."),
            };

            var topicKey = new TopicKey(serviceIdentifier, field);
            if (!_topicByField.TryGetValue(topicKey, out var topic))
            {
                topic = BuildTopic(publisher, serviceIdentifier, field);
                _topicByField[topicKey] = topic;
            }

            return publisher.PublishMessageAsync(topic,
                                                 Guid.NewGuid(),
                                                 cancellationToken,
                                                 MessageMimeTypes.Json,
                                                 schema,
                                                 payload,
                                                 MqttQualityOfServiceLevel.AtMostOnce,
                                                 true);
        }

        private static string BuildTopic(IServiceProviderPublisher publisher, string serviceIdentifier, IServiceField field)
        {
            var topicSuffix = field.Kind switch
            {
                ServiceFieldKind.Property => Topics.PropertyState,
                ServiceFieldKind.MeasuringPoint => Topics.MeasuringPointState,
                _ => throw new InvalidOperationException($"Unknown field kind '{field.Kind}' on field '{field.Name}'."),
            };

            var installationTopic = publisher.InstallationTopic ?? throw NotOperational(nameof(IServiceProviderPublisher.InstallationTopic));
            var serviceProviderIdentifier = publisher.ServiceProviderIdentifier ?? throw NotOperational(nameof(IServiceProviderPublisher.ServiceProviderIdentifier));

            return $"{installationTopic}/{serviceProviderIdentifier}/{serviceIdentifier}/{field.Name}{topicSuffix}";

            static InvalidOperationException NotOperational(string member)
            {
                return new
                    InvalidOperationException($"{member} is unavailable — the service provider is not operational yet. Publish service state from WithOnOperationalReady or a message handler, where the publish surface is operational.");
            }
        }

        private readonly record struct TopicKey(string ServiceIdentifier, IServiceField Field);
    }
}
