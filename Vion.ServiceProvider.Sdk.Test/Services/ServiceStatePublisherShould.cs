using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using MQTTnet.Protocol;
using Vion.Contracts.Conventions;
using Vion.Contracts.Events.ServiceProviderToMesh;
using Vion.Contracts.Mqtt;
using Vion.ServiceProvider.Sdk.RegistrationFlow;
using Vion.ServiceProvider.Sdk.Services;
using Vion.ServiceProvider.Sdk.Test.TestHelpers;

namespace Vion.ServiceProvider.Sdk.Test.Services
{
    [TestClass]
    public class ServiceStatePublisherShould
    {
        private static readonly string InstallationTopic = Guid.NewGuid().ToString();

        private static readonly string SpId = Guid.NewGuid().ToString();

        private static readonly string SvcId = Guid.NewGuid().ToString();

        private readonly Mock<IServiceProviderPublisher> _publisherMock = new();

        private ServiceStatePublisher _sut = null!;

        // A property field and a measuring-point field produce the same state message, differing only in the topic's kind segment and the envelope schema.
        private static IEnumerable<object[]> StateFields
        {
            get =>
            [
                [TestSchema.Plain, "property", nameof(PropertyStatePayload), JsonValue.Create(Guid.NewGuid().ToString())],
                [TestSchema.Reading, "measuringPoint", nameof(MeasuringPointStatePayload), JsonValue.Create(Random.Shared.NextDouble())],
            ];
        }

        [TestInitialize]
        public void Initialize()
        {
            _publisherMock.SetupGet(publisher => publisher.InstallationTopic).Returns(InstallationTopic);
            _publisherMock.SetupGet(publisher => publisher.ServiceProviderIdentifier).Returns(SpId);
            _sut = new ServiceStatePublisher();
        }

        [TestMethod]
        [DynamicData(nameof(StateFields))]
        public async Task PublishStateMessage(object field, string kindSegment, string expectedSchema, JsonNode value)
        {
            // Arrange
            var serviceField = (IServiceField)field;

            // Act
            var call = await PublishAsync(serviceField, value);

            // Assert — the publisher delegates to the strict publish surface, which stamps correlationId + published_at.
            Assert.AreEqual($"{InstallationTopic}/{SpId}/{SvcId}/{serviceField.Name}/sw/{kindSegment}/state", call.Topic);
            Assert.IsTrue(JsonNode.DeepEquals(value, DecodePayload(call.Payload)));
            Assert.AreEqual(MessageMimeTypes.Json, call.ContentType);
            Assert.AreEqual(expectedSchema, call.Schema);
            Assert.AreEqual(MqttQualityOfServiceLevel.AtMostOnce, call.Qos);
            Assert.IsTrue(call.Retain);
            Assert.AreNotEqual(Guid.Empty, call.CorrelationId);
        }

        [TestMethod]
        public async Task RedactWriteOnlyFieldValue()
        {
            // Arrange
            var value = Guid.NewGuid().ToString();

            // Act
            var call = await PublishAsync(TestSchema.Secret, value);

            // Assert
            Assert.AreEqual(WriteOnlyConventions.RedactedSentinel, DecodePayload(call.Payload)!.GetValue<string>());
        }

        [TestMethod]
        public async Task PublishNullForUnsetWriteOnlyField()
        {
            // Arrange

            // Act
            var call = await PublishAsync(TestSchema.Secret, null);

            // Assert
            Assert.IsNull(DecodePayload(call.Payload));
        }

        [TestMethod]
        public async Task UseFieldSpecificTopicForEachFieldOnTheSameInstance()
        {
            // Act — publish two different fields through the same (topic-caching) publisher instance.
            var propertyCall = await PublishAsync(TestSchema.Plain, JsonValue.Create(Guid.NewGuid().ToString()));
            var measuringPointCall = await PublishAsync(TestSchema.Reading, JsonValue.Create(Random.Shared.NextDouble()));

            // Assert — the cache keys per (serviceIdentifier, field), so distinct fields resolve to distinct, correct
            // topics rather than colliding on a single cached entry.
            Assert.AreEqual($"{InstallationTopic}/{SpId}/{SvcId}/{TestSchema.Plain.Name}/sw/property/state", propertyCall.Topic);
            Assert.AreEqual($"{InstallationTopic}/{SpId}/{SvcId}/{TestSchema.Reading.Name}/sw/measuringPoint/state", measuringPointCall.Topic);
        }

        private static JsonNode? DecodePayload(byte[] payload)
        {
            return JsonNode.Parse(payload)?["value"];
        }

        private async Task<PublishCall> PublishAsync(IServiceField field, JsonNode? value)
        {
            PublishCall captured = null!;
            _publisherMock.Setup(publisher => publisher.PublishMessageAsync(It.IsAny<string>(),
                                                                            It.IsAny<Guid>(),
                                                                            It.IsAny<CancellationToken>(),
                                                                            It.IsAny<string>(),
                                                                            It.IsAny<string>(),
                                                                            It.IsAny<ReadOnlyMemory<byte>>(),
                                                                            It.IsAny<MqttQualityOfServiceLevel>(),
                                                                            It.IsAny<bool>()))
                          .Callback((string topic,
                                     Guid correlationId,
                                     CancellationToken _,
                                     string? contentType,
                                     string? schema,
                                     ReadOnlyMemory<byte> payload,
                                     MqttQualityOfServiceLevel qos,
                                     bool retain) => captured = new PublishCall(topic,
                                                                                correlationId,
                                                                                contentType,
                                                                                schema,
                                                                                payload.ToArray(),
                                                                                qos,
                                                                                retain))
                          .ReturnsAsync(true);

            await _sut.PublishFieldAsync(_publisherMock.Object, SvcId, field, value, CancellationToken.None);
            return captured;
        }

        private sealed record PublishCall(
            string Topic,
            Guid CorrelationId,
            string? ContentType,
            string? Schema,
            byte[] Payload,
            MqttQualityOfServiceLevel Qos,
            bool Retain);
    }
}
