using System;
using Google.FlatBuffers;
using Vion.Contracts.FlatBuffers.System.Health;

namespace Vion.ServiceProvider.Sdk
{
    /// <summary>
    ///     Factory for building FlatBuffer-encoded payloads.
    /// </summary>
    public static class FlatBufferPayloadFactory
    {
        /// <summary>
        ///     Builds a serialized <see cref="ComponentHealthStatusPayload" /> FlatBuffer describing a single component's health.
        /// </summary>
        /// <param name="name">The component name — typically the service provider identifier.</param>
        /// <param name="connectionStatus">The component's connection status.</param>
        /// <param name="healthStatus">The component's health status.</param>
        /// <param name="since">The timestamp since when this status has been active, or <c>null</c> when unknown.</param>
        /// <returns>The serialized FlatBuffer payload.</returns>
        public static byte[] CreateComponentHealthStatusPayload(string name, ConnectionStatus connectionStatus, HealthStatus healthStatus, DateTime? since)
        {
            var flatBufferBuilder = new FlatBufferBuilder(72);

            var sinceUnixMs = since.HasValue ? new DateTimeOffset(since.Value).ToUnixTimeMilliseconds() : 0L;

            var nameOffset = flatBufferBuilder.CreateString(name);
            var componentOffset = Component.CreateComponent(flatBufferBuilder,
                                                            nameOffset,
                                                            connectionStatus,
                                                            healthStatus,
                                                            sinceUnixMs // sub_components: null
                                                           );

            var payloadOffset = ComponentHealthStatusPayload.CreateComponentHealthStatusPayload(flatBufferBuilder, componentOffset);
            ComponentHealthStatusPayload.FinishComponentHealthStatusPayloadBuffer(flatBufferBuilder, payloadOffset);

            return flatBufferBuilder.SizedByteArray();
        }
    }
}