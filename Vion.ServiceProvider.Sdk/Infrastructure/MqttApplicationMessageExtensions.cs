using System;
using System.Buffers;
using System.Buffers.Text;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Google.FlatBuffers;
using MQTTnet;
using MQTTnet.Packets;
using Vion.Contracts.Mqtt;

namespace Vion.ServiceProvider.Sdk.Infrastructure
{
    /// <summary>
    ///     Extension methods for reading VION's MQTT metadata (correlation ID, schema, payload) off an <see cref="MqttApplicationMessage" />.
    /// </summary>
    public static class MqttApplicationMessageExtensions
    {
        private static string[] SplitSegments(int segmentCount, ReadOnlySpan<char> segmentParts)
        {
            var segments = new string[segmentCount];
            var segmentIndex = 0;
            var segmentStart = 0;
            for (var i = 0; i <= segmentParts.Length; i++)
            {
                if (i != segmentParts.Length && segmentParts[i] != '/')
                {
                    continue;
                }

                if (segmentIndex == segments.Length)
                {
                    var actualSegments = segmentParts.Count('/') + 1; // +1 accounts for the first segment, which has no preceding slash due to Trim('/')
                    throw new UnexpectedSegmentCountException(segmentCount, actualSegments);
                }

                segments[segmentIndex++] = segmentParts.Slice(segmentStart, i - segmentStart).ToString();
                segmentStart = i + 1;
            }

            if (segmentIndex != segmentCount)
            {
                throw new UnexpectedSegmentCountException(segmentCount, segmentIndex);
            }

            return segments;
        }

        extension(MqttApplicationMessage message)
        {
            /// <summary>
            ///     Returns the installation topic portion of an MQTT topic.
            /// </summary>
            /// <returns>A span over the installation topic portion.</returns>
            /// <exception cref="InvalidInstallationTopicException">Thrown when the topic has fewer than four slash-delimited segments.</exception>
            /// <remarks>
            ///     The installation topic is always the first four slash-delimited segments: <c>version/environment/tenantId/gatewayId</c>.
            /// </remarks>
            public ReadOnlySpan<char> ExtractInstallationTopic()
            {
                var topic = message.Topic.AsSpan();
                var offset = 0;
                const int segmentCount = 4;
                for (var i = 0; i < segmentCount; i++)
                {
                    var nextSlash = topic[offset..].IndexOf('/');
                    if (nextSlash < 0)
                    {
                        throw new InvalidInstallationTopicException(segmentCount);
                    }

                    offset += nextSlash + 1; // +1 to skip past the slash
                }

                return topic[..(offset - 1)]; // -1 to exclude the trailing slash
            }

            /// <summary>
            ///     Extracts a fixed number of slash-delimited segments from an MQTT topic that appear between two substrings of the
            ///     topic.
            /// </summary>
            /// <param name="after">
            ///     The substring after which segment extraction begins. Pass <see cref="ReadOnlySpan{T}.Empty" /> to start extraction at the beginning of the topic.
            /// </param>
            /// <param name="before">
            ///     The substring before which segment extraction ends. Pass <see cref="ReadOnlySpan{T}.Empty" /> to extract until the end of the topic.
            /// </param>
            /// <param name="segmentCount">The exact number of segments expected between the two substrings.</param>
            /// <returns>An array containing the extracted segments.</returns>
            /// <exception cref="TopicSubstringNotFoundException">
            ///     Thrown when a non-empty <paramref name="after" /> or a non-empty <paramref name="before" /> cannot be located in the topic.
            /// </exception>
            /// <exception cref="UnexpectedSegmentCountException">
            ///     Thrown when the number of segments between the two substrings does not equal <paramref name="segmentCount" />.
            /// </exception>
            public string[] ExtractSegments(ReadOnlySpan<char> after, ReadOnlySpan<char> before, int segmentCount)
            {
                var topic = message.Topic.AsSpan();
                var sliceStart = 0;
                if (!after.IsEmpty)
                {
                    var startIndex = topic.IndexOf(after);
                    if (startIndex < 0)
                    {
                        throw new TopicSubstringNotFoundException(after.ToString());
                    }

                    sliceStart = startIndex + after.Length;
                }

                var sliceEnd = topic.Length;
                if (!before.IsEmpty)
                {
                    var relativeEndIndex = topic[sliceStart..].IndexOf(before); // Only search past the `after` match so `before` doesn't match something inside `after`
                    if (relativeEndIndex < 0)
                    {
                        throw new TopicSubstringNotFoundException(before.ToString());
                    }

                    sliceEnd = sliceStart + relativeEndIndex;
                }

                return SplitSegments(segmentCount, topic[sliceStart..sliceEnd].Trim('/'));
            }

            /// <summary>
            ///     Retrieves the correlation ID from the <see cref="MqttApplicationMessage.CorrelationData" />.
            /// </summary>
            /// <returns>The extracted correlation ID as a <see cref="Guid" />.</returns>
            /// <exception cref="MissingCorrelationIdException">Thrown if <see cref="MqttApplicationMessage.CorrelationData" /> is <c>null</c>.</exception>
            /// <exception cref="InvalidCorrelationIdFormatException">Thrown if the correlation ID is not in a supported format (16-byte array or 36-character string).</exception>
            public Guid GetCorrelationId()
            {
                if (message.CorrelationData == null)
                {
                    throw new MissingCorrelationIdException();
                }

                return message.CorrelationData.Length switch
                {
                    16 => new Guid(message.CorrelationData),
                    36 when Utf8Parser.TryParse(message.CorrelationData, out Guid correlationId, out _) => correlationId,
                    _ => throw new InvalidCorrelationIdFormatException(message.CorrelationData),
                };
            }

            /// <summary>
            ///     Reads the W3C <c>traceparent</c> user property carried for cross-hop trace propagation.
            /// </summary>
            /// <returns>The <c>traceparent</c> value, or <c>null</c> if the message carries none.</returns>
            public ReadOnlyMemory<byte>? GetTraceParent()
            {
                return message.UserProperties?.Find(property => property.Name == MqttUserProperties.TraceParent.Name)?.ValueBuffer;
            }

            /// <summary>
            ///     Extracts the payload of an MQTT message as a <see cref="ByteBuffer" /> for the caller to deserialize.
            /// </summary>
            /// <param name="expectedSchema">
            ///     The expected payload schema identifier used for validation. This typically matches the
            ///     FlatBuffer table type name.
            /// </param>
            /// <returns>A <see cref="ByteBuffer" /> containing the message payload, positioned at the start of the FlatBuffer object.</returns>
            /// <exception cref="PayloadEmptyException">Thrown if the message payload is empty.</exception>
            /// <exception cref="UnsupportedContentTypeException">Thrown if the message content type is not <c>application/x-flatbuffers</c>.</exception>
            /// <exception cref="MissingPayloadSchemaException">Thrown if the schema property is missing in the MQTT user properties.</exception>
            /// <exception cref="InvalidPayloadSchemaException">Thrown if the schema does not match <paramref name="expectedSchema" />.</exception>
            public ByteBuffer GetFlatBufferPayload(string expectedSchema)
            {
                message.EnsureExpectedPayloadContract(MessageMimeTypes.FlatBuffer, expectedSchema);
                if (message.Payload.IsSingleSegment && MemoryMarshal.TryGetArray(message.Payload.First, out var segment))
                {
                    return new ByteBuffer(segment.Array, segment.Offset);
                }

                return new ByteBuffer(message.Payload.ToArray());
            }

            /// <summary>
            ///     Deserializes the payload of an MQTT message as a JSON-encoded object of type <typeparamref name="T" />.
            /// </summary>
            /// <typeparam name="T">The expected deserialized type.</typeparam>
            /// <param name="typeInfo">The type metadata used by the source generator for JSON deserialization.</param>
            /// <param name="expectedSchemaOverride">
            ///     Optionally overrides the expected schema name used to validate the payload.
            ///     If not provided, the name of the type <typeparamref name="T" /> is used.
            /// </param>
            /// <returns>The deserialized object of type <typeparamref name="T" />.</returns>
            /// <exception cref="PayloadEmptyException">Thrown if the message payload is empty.</exception>
            /// <exception cref="UnsupportedContentTypeException">Thrown if the message content type is not <c>application/json</c>.</exception>
            /// <exception cref="MissingPayloadSchemaException">Thrown if the schema property is missing in the message user properties.</exception>
            /// <exception cref="InvalidPayloadSchemaException">Thrown if the schema from the message does not match the expected schema.</exception>
            /// <exception cref="PayloadDeserializationException">Thrown if an exception occurs during JSON deserialization.</exception>
            /// <exception cref="PayloadNullAfterDeserializationException">Thrown if the deserialized object is null.</exception>
            public T GetJsonPayload<T>(JsonTypeInfo<T> typeInfo, string? expectedSchemaOverride = null)
            {
                message.EnsureExpectedPayloadContract(MessageMimeTypes.Json, expectedSchemaOverride ?? typeof(T).Name);

                T? deserialized;
                try
                {
                    if (message.Payload.IsSingleSegment)
                    {
                        deserialized = JsonSerializer.Deserialize(message.Payload.FirstSpan, typeInfo);
                    }
                    else
                    {
                        var reader = new Utf8JsonReader(message.Payload);
                        deserialized = JsonSerializer.Deserialize(ref reader, typeInfo);
                    }
                }
                catch (Exception exception)
                {
                    throw new PayloadDeserializationException(exception);
                }

                if (deserialized == null)
                {
                    throw new PayloadNullAfterDeserializationException();
                }

                return deserialized;
            }

            /// <summary>
            ///     Ensures that the MQTT message payload matches the expected payload contract.
            /// </summary>
            /// <param name="expectedContentType">The expected content type of the message payload.</param>
            /// <param name="expectedSchema">The expected schema identifier of the message payload.</param>
            /// <exception cref="PayloadEmptyException">Thrown if the message payload is empty.</exception>
            /// <exception cref="UnsupportedContentTypeException">Thrown if the message content type does not match <paramref name="expectedContentType" />.</exception>
            /// <exception cref="MissingPayloadSchemaException">Thrown if the schema user property is missing.</exception>
            /// <exception cref="InvalidPayloadSchemaException">Thrown if the schema user property does not match <paramref name="expectedSchema" />.</exception>
            public void EnsureExpectedPayloadContract(string expectedContentType, string expectedSchema)
            {
                message.EnsurePayloadNotEmpty();
                message.EnsureValidContentType(expectedContentType);
                message.EnsureValidSchema(expectedSchema);
            }

            private void EnsurePayloadNotEmpty()
            {
                if (message.Payload.IsEmpty)
                {
                    throw new PayloadEmptyException();
                }
            }

            private void EnsureValidContentType(string expectedContentType)
            {
                var contentType = message.ContentType;
                if (contentType != expectedContentType)
                {
                    throw new UnsupportedContentTypeException(expectedContentType, contentType);
                }
            }

            private void EnsureValidSchema(string expectedSchema)
            {
                var schema = message.UserProperties?.Find(property => property.Name == MqttUserProperties.Schema.Name)?.ReadValueAsString();
                if (schema == null)
                {
                    throw new MissingPayloadSchemaException();
                }

                if (schema != expectedSchema)
                {
                    throw new InvalidPayloadSchemaException(expectedSchema, schema);
                }
            }
        }
    }

    /// <summary>Thrown when an MQTT topic has fewer slash-delimited segments than an installation topic requires.</summary>
    public sealed class InvalidInstallationTopicException : Exception
    {
        /// <summary>Initializes the exception for a topic missing the expected installation-topic segments.</summary>
        /// <param name="expectedSegmentCount">The number of leading segments an installation topic requires.</param>
        public InvalidInstallationTopicException(int expectedSegmentCount) :
            base($"Topic has fewer than {expectedSegmentCount} slash-delimited segments; cannot extract installation topic.")
        {
        }
    }

    /// <summary>Thrown when an expected substring cannot be located within an MQTT topic during segment extraction.</summary>
    public sealed class TopicSubstringNotFoundException : Exception
    {
        /// <summary>Initializes the exception for a substring that was not found in the topic.</summary>
        /// <param name="substring">The substring that was expected but not found.</param>
        public TopicSubstringNotFoundException(string substring) : base($"Expected substring '{substring}' was not found in topic.")
        {
        }
    }

    /// <summary>Thrown when the number of extracted topic segments does not match the expected count.</summary>
    public sealed class UnexpectedSegmentCountException : Exception
    {
        /// <summary>Initializes the exception with the expected and actual segment counts.</summary>
        /// <param name="expectedCount">The expected number of segments.</param>
        /// <param name="actualCount">The actual number of segments found.</param>
        public UnexpectedSegmentCountException(int expectedCount, int actualCount) : base($"Expected {expectedCount} segment(s), but got {actualCount}.")
        {
        }
    }

    /// <summary>Thrown when a received message carries no correlation ID in its <see cref="MqttApplicationMessage.CorrelationData" />.</summary>
    public sealed class MissingCorrelationIdException : Exception
    {
        /// <summary>Initializes the exception for a message with no correlation data.</summary>
        public MissingCorrelationIdException() : base("Received message without a correlation ID.")
        {
        }
    }

    /// <summary>Thrown when correlation data is present but not a supported correlation-ID format (16-byte array or 36-character string).</summary>
    public sealed class InvalidCorrelationIdFormatException : Exception
    {
        /// <summary>Initializes the exception for correlation data that is not a recognized correlation-ID format.</summary>
        /// <param name="correlationData">The unparseable correlation data.</param>
        public InvalidCorrelationIdFormatException(byte[] correlationData) :
            base($"Received message with invalid correlation ID format — byte array length was {correlationData.Length}.")
        {
        }
    }

    /// <summary>Thrown when a received message has an empty payload where one is required.</summary>
    public sealed class PayloadEmptyException : Exception
    {
        /// <summary>Initializes the exception for an empty payload.</summary>
        public PayloadEmptyException() : base("Received empty payload — a payload is required.")
        {
        }
    }

    /// <summary>Thrown when a received message's content type does not match the expected content type.</summary>
    public sealed class UnsupportedContentTypeException : Exception
    {
        /// <summary>Initializes the exception with the expected and actual content types.</summary>
        /// <param name="expectedContentType">The content type the caller expected.</param>
        /// <param name="actualContentType">The content type actually present on the message.</param>
        public UnsupportedContentTypeException(string expectedContentType, string? actualContentType) :
            base($"Received message with unsupported content type — expected '{expectedContentType}' but got '{actualContentType}'.")
        {
        }
    }

    /// <summary>Thrown when a received message is missing the <c>schema</c> user property.</summary>
    public sealed class MissingPayloadSchemaException : Exception
    {
        /// <summary>Initializes the exception for a message with no schema user property.</summary>
        public MissingPayloadSchemaException() : base("Received message without payload schema.")
        {
        }
    }

    /// <summary>Thrown when a received message's <c>schema</c> user property does not match the expected schema.</summary>
    public sealed class InvalidPayloadSchemaException : Exception
    {
        /// <summary>Initializes the exception with the expected and actual schema names.</summary>
        /// <param name="expectedSchema">The schema the caller expected.</param>
        /// <param name="actualSchema">The schema actually present on the message.</param>
        public InvalidPayloadSchemaException(string expectedSchema, string actualSchema) :
            base($"Received message with invalid payload schema — expected '{expectedSchema}', actual '{actualSchema}'.")
        {
        }
    }

    /// <summary>Thrown when a received message's payload cannot be deserialized.</summary>
    public sealed class PayloadDeserializationException : Exception
    {
        /// <summary>Initializes the exception wrapping the underlying deserialization failure.</summary>
        /// <param name="innerException">The exception thrown during deserialization.</param>
        public PayloadDeserializationException(Exception innerException) : base("Received message with a payload that could not be deserialized.", innerException)
        {
        }
    }

    /// <summary>Thrown when a received message's payload deserializes to <c>null</c>.</summary>
    public sealed class PayloadNullAfterDeserializationException : Exception
    {
        /// <summary>Initializes the exception for a payload that was null after deserialization.</summary>
        public PayloadNullAfterDeserializationException() : base("Received message with a payload that was null after deserialization.")
        {
        }
    }
}