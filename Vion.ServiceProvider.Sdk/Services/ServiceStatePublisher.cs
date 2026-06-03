using System;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet.Protocol;
using Vion.Contracts.Codec;
using Vion.Contracts.Conventions;
using Vion.Contracts.FlatBuffers.Common;
using Vion.Contracts.Mqtt;
using Vion.ServiceProvider.Sdk.RegistrationFlow;

namespace Vion.ServiceProvider.Sdk.Services
{
    /// <summary>
    ///     Publishes service-field values as retained <c>PropertyValue</c> FlatBuffers on the field's state topic.
    /// </summary>
    public sealed class ServiceStatePublisher : IServiceStatePublisher
    {
        private readonly string _installationTopic;

        private readonly string _serviceProviderIdentifier;

        private readonly ConcurrentDictionary<TopicKey, string> _topicByField = new();

        /// <summary>Initializes a new instance of the <see cref="ServiceStatePublisher" /> class.</summary>
        /// <param name="installationTopic">The installation topic, known after registration acceptance.</param>
        /// <param name="serviceProviderIdentifier">This service provider's identifier.</param>
        public ServiceStatePublisher(string installationTopic, string serviceProviderIdentifier)
        {
            _installationTopic = installationTopic;
            _serviceProviderIdentifier = serviceProviderIdentifier;
        }

        /// <inheritdoc />
        public Task PublishFieldAsync(IServiceProviderPublish publisher, string serviceIdentifier, IServiceField field, JsonNode? value, CancellationToken cancellationToken)
        {
            var publishedValue = field.IsWriteOnly && value is not null ? JsonValue.Create(WriteOnlyConventions.RedactedSentinel) : value;
            var payload = PropertyValueCodec.JsonToFlatBuffer(publishedValue, field.Schema.Type);

            var topicKey = new TopicKey(serviceIdentifier, field);
            if (!_topicByField.TryGetValue(topicKey, out var topic))
            {
                topic = BuildTopic(serviceIdentifier, field);
                _topicByField[topicKey] = topic;
            }

            return publisher.PublishMessageAsync(topic,
                                                 Guid.NewGuid(),
                                                 cancellationToken,
                                                 MessageMimeTypes.FlatBuffer,
                                                 nameof(PropertyValue),
                                                 payload,
                                                 MqttQualityOfServiceLevel.AtMostOnce,
                                                 true);
        }

        private string BuildTopic(string serviceIdentifier, IServiceField field)
        {
            var topicSuffix = field.Kind switch
            {
                ServiceFieldKind.Property => Topics.PropertyState,
                ServiceFieldKind.MeasuringPoint => Topics.MeasuringPointState,
                _ => throw new InvalidOperationException($"Unknown field kind '{field.Kind}' on field '{field.Name}'."),
            };

            return $"{_installationTopic}/{_serviceProviderIdentifier}/{serviceIdentifier}/{field.Name}{topicSuffix}";
        }

        private readonly record struct TopicKey(string ServiceIdentifier, IServiceField Field);
    }
}