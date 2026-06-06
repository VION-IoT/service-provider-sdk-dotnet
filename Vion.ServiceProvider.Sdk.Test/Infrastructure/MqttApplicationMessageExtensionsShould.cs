using System;
using System.Buffers;
using System.Text;
using System.Text.Json;
using MQTTnet;
using Vion.Contracts.Mqtt;
using Vion.ServiceProvider.Sdk.Infrastructure;
using Vion.ServiceProvider.Sdk.Test.TestHelpers;
using MqttApplicationMessageBuilder = Vion.ServiceProvider.Sdk.Test.TestHelpers.MqttApplicationMessageBuilder;
using MqttUserProperty = MQTTnet.Packets.MqttUserProperty;

namespace Vion.ServiceProvider.Sdk.Test.Infrastructure
{
    [TestClass]
    public class MqttApplicationMessageExtensionsShould
    {
        private static readonly string Topic = Guid.NewGuid().ToString();

        private static readonly string Schema = Guid.NewGuid().ToString();

        private static readonly byte[] Payload = Guid.NewGuid().ToByteArray();

        [TestMethod]
        public void ReturnInstallationTopic()
        {
            // Arrange
            var message = new MqttApplicationMessage { Topic = "v1/dev/tenant-x/gateway-99/dale/svc/prop/sw/property/state" };

            // Act
            var installationTopic = message.ExtractInstallationTopic();

            // Assert
            Assert.AreEqual("v1/dev/tenant-x/gateway-99", installationTopic.ToString());
        }

        [TestMethod]
        public void ThrowWhenTopicHasTooFewSegments()
        {
            // Arrange
            var message = new MqttApplicationMessage { Topic = "v1/dev/tenant-x" };

            // Act / Assert
            Assert.Throws<InvalidInstallationTopicException>(() => message.ExtractInstallationTopic());
        }

        [TestMethod]
        public void ReturnSegmentsBetweenInstallationTopicAndStateSuffix()
        {
            // Arrange
            const string topic = "v1/dev/tenant-x/gateway-99/dale/logic-block/voltage/sw/property/state";
            var message = new MqttApplicationMessage { Topic = topic };

            // Act
            var actualSegments = message.ExtractSegments("v1/dev/tenant-x/gateway-99", "/sw/property/state", 3);

            // Assert
            string[] expectedSegments = ["dale", "logic-block", "voltage"];
            CollectionAssert.AreEqual(expectedSegments, actualSegments);
        }

        [TestMethod]
        public void ThrowWhenAfterSubstringNotFound()
        {
            // Arrange
            const string topic = "v1/dev/tenant-x/gateway-99/dale/logic-block/voltage/sw/property/state";
            var message = new MqttApplicationMessage { Topic = topic };

            // Act / Assert
            Assert.Throws<TopicSubstringNotFoundException>(() => message.ExtractSegments("missing/", "/sw/property/state", 3));
        }

        [TestMethod]
        public void ThrowWhenBeforeSubstringNotFound()
        {
            // Arrange
            const string topic = "v1/dev/tenant-x/gateway-99/dale/logic-block/voltage/sw/property/state";
            var message = new MqttApplicationMessage { Topic = topic };

            // Act / Assert
            Assert.Throws<TopicSubstringNotFoundException>(() => message.ExtractSegments("v1/dev/tenant-x/gateway-99", "/missing", 3));
        }

        [TestMethod]
        [DataRow(2, DisplayName = "Expecting fewer segments than the topic has")]
        [DataRow(4, DisplayName = "Expecting more segments than the topic has")]
        public void ThrowWhenSegmentCountDoesNotMatch(int segmentCount)
        {
            // Arrange
            const string topic = "v1/dev/tenant-x/gateway-99/dale/logic-block/voltage/sw/property/state";
            var message = new MqttApplicationMessage { Topic = topic };

            // Act / Assert
            Assert.Throws<UnexpectedSegmentCountException>(() => message.ExtractSegments("v1/dev/tenant-x/gateway-99", "/sw/property/state", segmentCount));
        }

        [TestMethod]
        public void ThrowWhenCorrelationDataIsNull()
        {
            // Arrange
            var message = new MqttApplicationMessage();

            // Act / Assert
            Assert.Throws<MissingCorrelationIdException>(() => message.GetCorrelationId());
        }

        [TestMethod]
        public void ReturnCorrelationIdFromBinaryGuid()
        {
            // Arrange
            var expectedCorrelationId = Guid.NewGuid();
            var message = new MqttApplicationMessage { CorrelationData = expectedCorrelationId.ToByteArray() };

            // Act
            var actualCorrelationId = message.GetCorrelationId();

            // Assert
            Assert.AreEqual(expectedCorrelationId, actualCorrelationId);
        }

        [TestMethod]
        public void ReturnCorrelationIdFromStringGuid()
        {
            // Arrange
            var expectedCorrelationId = Guid.NewGuid();
            var message = new MqttApplicationMessage { CorrelationData = Encoding.UTF8.GetBytes(expectedCorrelationId.ToString()) };

            // Act
            var actualCorrelationId = message.GetCorrelationId();

            // Assert
            Assert.AreEqual(expectedCorrelationId, actualCorrelationId);
        }

        [TestMethod]
        public void ThrowWhenCorrelationDataLengthIsUnsupported()
        {
            // Arrange
            var message = new MqttApplicationMessage { CorrelationData = new byte[10] };

            // Act / Assert
            Assert.Throws<InvalidCorrelationIdFormatException>(() => message.GetCorrelationId());
        }

        [TestMethod]
        public void ThrowWhenCorrelationDataIsNotAValidGuid()
        {
            // Arrange
            var message = new MqttApplicationMessage { CorrelationData = Encoding.UTF8.GetBytes(new string('x', 36)) };

            // Act / Assert
            Assert.Throws<InvalidCorrelationIdFormatException>(() => message.GetCorrelationId());
        }

        [TestMethod]
        public void ReturnTraceParent()
        {
            // Arrange
            var traceParent = Guid.NewGuid().ToString();
            var message = new MqttApplicationMessage
                          {
                              Topic = Topic,
                              UserProperties = [new MqttUserProperty(MqttUserProperties.TraceParent.Name, Encoding.UTF8.GetBytes(traceParent))],
                          };

            // Act
            var actualTraceParent = message.GetTraceParent();

            // Assert
            Assert.AreEqual(traceParent, Encoding.UTF8.GetString(actualTraceParent!.Value.Span));
        }

        [TestMethod]
        public void ReturnNullTraceParentWhenMissing()
        {
            // Arrange
            var message = new MqttApplicationMessage { Topic = Topic };

            // Act
            var traceParent = message.GetTraceParent();

            // Assert
            Assert.IsNull(traceParent);
        }

        [TestMethod]
        public void ThrowWhenFlatBufferPayloadIsEmpty()
        {
            // Arrange
            var message = MqttApplicationMessageBuilder.BuildEmptyPayload(Topic);

            // Act / Assert
            Assert.Throws<PayloadEmptyException>(() => message.GetFlatBufferPayload(Schema));
        }

        [TestMethod]
        public void ThrowWhenContentTypeIsNotFlatBuffer()
        {
            // Arrange
            var message = MqttApplicationMessageBuilder.BuildJson(Topic, Payload, Schema);

            // Act / Assert
            Assert.Throws<UnsupportedContentTypeException>(() => message.GetFlatBufferPayload(Schema));
        }

        [TestMethod]
        public void ThrowWhenFlatBufferSchemaIsMissing()
        {
            // Arrange
            var message = MqttApplicationMessageBuilder.BuildFlatBuffer(Topic, Payload, Schema);
            message.UserProperties = null;

            // Act / Assert
            Assert.Throws<MissingPayloadSchemaException>(() => message.GetFlatBufferPayload(Schema));
        }

        [TestMethod]
        public void ThrowWhenFlatBufferSchemaDoesNotMatch()
        {
            // Arrange
            var message = MqttApplicationMessageBuilder.BuildFlatBuffer(Topic, Payload, Schema);

            // Act / Assert
            Assert.Throws<InvalidPayloadSchemaException>(() => message.GetFlatBufferPayload(Guid.NewGuid().ToString()));
        }

        [TestMethod]
        public void ReturnFlatBufferPayload()
        {
            // Arrange
            var message = MqttApplicationMessageBuilder.BuildFlatBuffer(Topic, Payload, Schema);

            // Act
            var extractedPayload = message.GetFlatBufferPayload(Schema);

            // Assert
            CollectionAssert.AreEqual(Payload, extractedPayload.ToSizedArray());
        }

        [TestMethod]
        public void ReturnFlatBufferPayloadWhenPayloadSpansMultipleSegments()
        {
            // Arrange
            var message = MqttApplicationMessageBuilder.BuildFlatBuffer(Topic, CreateMultiSegmentSequence(Payload), Schema);

            // Act
            var extractedPayload = message.GetFlatBufferPayload(Schema);

            // Assert
            CollectionAssert.AreEqual(Payload, extractedPayload.ToSizedArray());
        }

        [TestMethod]
        public void ThrowWhenJsonPayloadIsEmpty()
        {
            // Arrange
            var message = MqttApplicationMessageBuilder.BuildEmptyPayload(Topic);

            // Act / Assert
            Assert.Throws<PayloadEmptyException>(() => message.GetJsonPayload(TestServiceStateJsonContext.Default.TestServiceState));
        }

        [TestMethod]
        public void ThrowWhenContentTypeIsNotJson()
        {
            // Arrange
            var message = MqttApplicationMessageBuilder.BuildFlatBuffer(Topic, Payload, nameof(TestServiceState));

            // Act / Assert
            Assert.Throws<UnsupportedContentTypeException>(() => message.GetJsonPayload(TestServiceStateJsonContext.Default.TestServiceState));
        }

        [TestMethod]
        public void ThrowWhenJsonSchemaIsMissing()
        {
            // Arrange
            var message = MqttApplicationMessageBuilder.BuildJson(Topic, Payload, nameof(TestServiceState));
            message.UserProperties = null;

            // Act / Assert
            Assert.Throws<MissingPayloadSchemaException>(() => message.GetJsonPayload(TestServiceStateJsonContext.Default.TestServiceState));
        }

        [TestMethod]
        public void ThrowWhenJsonSchemaDoesNotMatch()
        {
            // Arrange
            var message = MqttApplicationMessageBuilder.BuildJson(Topic, Payload, Guid.NewGuid().ToString());

            // Act / Assert
            Assert.Throws<InvalidPayloadSchemaException>(() => message.GetJsonPayload(TestServiceStateJsonContext.Default.TestServiceState));
        }

        [TestMethod]
        public void ThrowWhenPayloadIsNotValidJson()
        {
            // Arrange
            var message = MqttApplicationMessageBuilder.BuildJson(Topic, "{ invalid json"u8.ToArray(), nameof(TestServiceState));

            // Act / Assert
            Assert.Throws<PayloadDeserializationException>(() => message.GetJsonPayload(TestServiceStateJsonContext.Default.TestServiceState));
        }

        [TestMethod]
        public void ThrowWhenPayloadDeserializesToNull()
        {
            // Arrange
            var message = MqttApplicationMessageBuilder.BuildJson(Topic, "null"u8.ToArray(), nameof(TestServiceState));

            // Act / Assert
            Assert.Throws<PayloadNullAfterDeserializationException>(() => message.GetJsonPayload(TestServiceStateJsonContext.Default.TestServiceState));
        }

        [TestMethod]
        public void ReturnJsonPayload()
        {
            // Arrange
            var expectedPayload = new TestServiceState(Guid.NewGuid().ToString(), Reading: Random.Shared.NextDouble());
            var serializedPayload = JsonSerializer.SerializeToUtf8Bytes(expectedPayload, TestServiceStateJsonContext.Default.TestServiceState);
            var message = MqttApplicationMessageBuilder.BuildJson(Topic, serializedPayload, nameof(TestServiceState));

            // Act
            var actualPayload = message.GetJsonPayload(TestServiceStateJsonContext.Default.TestServiceState);

            // Assert
            Assert.AreEqual(expectedPayload, actualPayload);
        }

        [TestMethod]
        public void ReturnJsonPayloadWhenPayloadSpansMultipleSegments()
        {
            // Arrange
            var expectedPayload = new TestServiceState(Guid.NewGuid().ToString(), Reading: Random.Shared.NextDouble());
            var serializedPayload = JsonSerializer.SerializeToUtf8Bytes(expectedPayload, TestServiceStateJsonContext.Default.TestServiceState);
            var message = MqttApplicationMessageBuilder.BuildJson(Topic, CreateMultiSegmentSequence(serializedPayload), nameof(TestServiceState));

            // Act
            var actualPayload = message.GetJsonPayload(TestServiceStateJsonContext.Default.TestServiceState);

            // Assert
            Assert.AreEqual(expectedPayload, actualPayload);
        }

        [TestMethod]
        public void ReturnJsonPayloadUsingSchemaOverride()
        {
            // Arrange
            var expectedPayload = new TestServiceState(Guid.NewGuid().ToString(), Reading: Random.Shared.NextDouble());
            var serializedPayload = JsonSerializer.SerializeToUtf8Bytes(expectedPayload, TestServiceStateJsonContext.Default.TestServiceState);
            var schemaOverride = Guid.NewGuid().ToString();
            var message = MqttApplicationMessageBuilder.BuildJson(Topic, serializedPayload, schemaOverride);

            // Act
            var actualPayload = message.GetJsonPayload(TestServiceStateJsonContext.Default.TestServiceState, schemaOverride);

            // Assert
            Assert.AreEqual(expectedPayload, actualPayload);
        }

        [TestMethod]
        public void AcceptPayloadWhenContentTypeAndSchemaMatch()
        {
            // Arrange
            var contentType = Guid.NewGuid().ToString();
            var message = MqttApplicationMessageBuilder.BuildWithContentType(Topic, Payload, contentType, Schema);

            // Act / Assert
            message.EnsureExpectedPayloadContract(contentType, Schema);
        }

        [TestMethod]
        public void ThrowWhenPayloadIsEmpty()
        {
            // Arrange
            var message = MqttApplicationMessageBuilder.BuildEmptyPayload(Topic);

            // Act / Assert
            Assert.Throws<PayloadEmptyException>(() => message.EnsureExpectedPayloadContract(Guid.NewGuid().ToString(), Guid.NewGuid().ToString()));
        }

        [TestMethod]
        public void ThrowWhenContentTypeDoesNotMatchExpected()
        {
            // Arrange
            var message = MqttApplicationMessageBuilder.BuildWithContentType(Topic, Payload, Guid.NewGuid().ToString(), Schema);

            // Act / Assert
            Assert.Throws<UnsupportedContentTypeException>(() => message.EnsureExpectedPayloadContract(Guid.NewGuid().ToString(), Schema));
        }

        [TestMethod]
        public void ThrowWhenSchemaDoesNotMatchExpected()
        {
            // Arrange
            var contentType = Guid.NewGuid().ToString();
            var message = MqttApplicationMessageBuilder.BuildWithContentType(Topic, Payload, contentType, Guid.NewGuid().ToString());

            // Act / Assert
            Assert.Throws<InvalidPayloadSchemaException>(() => message.EnsureExpectedPayloadContract(contentType, Guid.NewGuid().ToString()));
        }

        private static ReadOnlySequence<byte> CreateMultiSegmentSequence(byte[] payload)
        {
            var midpoint = payload.Length / 2;
            var firstSegment = new PayloadSegment(payload.AsMemory(0, midpoint));
            var lastSegment = new PayloadSegment(payload.AsMemory(midpoint), firstSegment);

            return new ReadOnlySequence<byte>(firstSegment, 0, lastSegment, lastSegment.Memory.Length);
        }

        private sealed class PayloadSegment : ReadOnlySequenceSegment<byte>
        {
            public PayloadSegment(ReadOnlyMemory<byte> memory, PayloadSegment? previous = null)
            {
                Memory = memory;
                if (previous is null)
                {
                    return;
                }

                previous.Next = this;
                RunningIndex = previous.RunningIndex + previous.Memory.Length;
            }
        }
    }
}
