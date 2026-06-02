using System;
using System.Buffers;
using System.Text;
using MQTTnet;
using Vion.Contracts.Mqtt;
using MqttUserProperty = MQTTnet.Packets.MqttUserProperty;

namespace Vion.ServiceProvider.Sdk.Test.TestHelpers
{
    public static class MqttApplicationMessageBuilder
    {
        public static MqttApplicationMessage BuildFlatBuffer(string topic, byte[] payload, string schema, Guid? correlationId = null)
        {
            return BuildFlatBuffer(topic, new ReadOnlySequence<byte>(payload), schema, correlationId);
        }

        public static MqttApplicationMessage BuildFlatBuffer(string topic, ReadOnlySequence<byte> payload, string schema, Guid? correlationId = null)
        {
            return Build(topic, payload, MessageMimeTypes.FlatBuffer, schema, correlationId);
        }

        public static MqttApplicationMessage BuildJson(string topic, byte[] payload, string schema, Guid? correlationId = null)
        {
            return BuildJson(topic, new ReadOnlySequence<byte>(payload), schema, correlationId);
        }

        public static MqttApplicationMessage BuildJson(string topic, ReadOnlySequence<byte> payload, string schema, Guid? correlationId = null)
        {
            return Build(topic, payload, MessageMimeTypes.Json, schema, correlationId);
        }

        public static MqttApplicationMessage BuildEmptyPayload(string topic, Guid? correlationId = null)
        {
            return new MqttApplicationMessage
                   {
                       Topic = topic,
                       Payload = new ReadOnlySequence<byte>([]),
                       CorrelationData = (correlationId ?? Guid.NewGuid()).ToByteArray(),
                   };
        }

        public static MqttApplicationMessage BuildWithContentType(string topic, byte[] payload, string contentType, string schema, Guid? correlationId = null)
        {
            return Build(topic, new ReadOnlySequence<byte>(payload), contentType, schema, correlationId);
        }

        private static MqttApplicationMessage Build(string topic, ReadOnlySequence<byte> payload, string contentType, string schema, Guid? correlationId)
        {
            return new MqttApplicationMessage
                   {
                       Topic = topic,
                       Payload = payload,
                       ContentType = contentType,
                       CorrelationData = (correlationId ?? Guid.NewGuid()).ToByteArray(),
                       UserProperties = [new MqttUserProperty(MqttUserProperties.Schema.Name, Encoding.UTF8.GetBytes(schema))],
                   };
        }
    }
}