using System;
using Google.FlatBuffers;
using Vion.Contracts.FlatBuffers.System.Health;

namespace Vion.ServiceProvider.Sdk
{
    public static class FlatBufferPayloadFactory
    {
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