using System;
using Google.FlatBuffers;
using Shared.Contracts.FlatBuffers.System.Health;

namespace Vion.ServiceProvider.Sdk
{
    public static class FlatBufferPayloadFactory
    {
        // Effective serialized sizes of scalar fields when embedded alongside other fields in a table.
        // These are not the raw primitive sizes (bool=1B, double=8B), but the measured space FlatBuffers
        // consumes due to:
        //   - Field alignment and padding to 4- or 8-byte boundaries
        //   - Table layout and vtable offset alignment
        //
        // Note:
        //   - When a table contains only a single scalar field (e.g., just a bool or double), its total
        //     size is smaller (≈20B for bool, ≈24B for double).
        //   - These constants represent the worst-case embedded size when the scalar appears alongside other fields.
        //private const int EmbeddedBoolSize = 24;

        //private const int EmbeddedDoubleSize = 32;

        //public static byte[] CreateDiStatePayload(string contractIdentifier, string serviceProviderIdentifier, bool value)
        //{
        //    const int payloadTableOverhead = 24; // includes vtable + table header + padding + length-prefix metadata
        //    var totalBytes = contractIdentifier.Length + serviceProviderIdentifier.Length + EmbeddedBoolSize + payloadTableOverhead;
        //    var initialSize = RoundUpTo8ByteAlignment(totalBytes);
        //    var builder = new FlatBufferBuilder(initialSize);
        //    var endpointOffset = builder.CreateString(contractIdentifier);
        //    var hwBlockOffset = builder.CreateString(serviceProviderIdentifier);
        //    var payloadOffset = DiStatePayload.CreateDiStatePayload(builder, endpointOffset, hwBlockOffset, value);
        //    DiStatePayload.FinishDiStatePayloadBuffer(builder, payloadOffset);

        //    return builder.SizedByteArray();
        //}

        //public static byte[] CreateAiStatePayload(string contractIdentifier, string serviceProviderIdentifier, double value)
        //{
        //    const int payloadTableOverhead = 24; // includes vtable + table header + padding + length-prefix metadata
        //    var totalBytes = contractIdentifier.Length + serviceProviderIdentifier.Length + EmbeddedDoubleSize + payloadTableOverhead;
        //    var initialSize = RoundUpTo8ByteAlignment(totalBytes);
        //    var builder = new FlatBufferBuilder(initialSize);
        //    var endpointOffset = builder.CreateString(contractIdentifier);
        //    var hwBlockOffset = builder.CreateString(serviceProviderIdentifier);
        //    var payloadOffset = AiStatePayload.CreateAiStatePayload(builder, endpointOffset, hwBlockOffset, value);
        //    AiStatePayload.FinishAiStatePayloadBuffer(builder, payloadOffset);

        //    return builder.SizedByteArray();
        //}

        //public static byte[] CreateDoStatePayload(string contractIdentifier, string serviceProviderIdentifier, bool value)
        //{
        //    const int payloadTableOverhead = 24; // includes vtable + table header + padding + length-prefix metadata
        //    var totalBytes = contractIdentifier.Length + serviceProviderIdentifier.Length + EmbeddedBoolSize + payloadTableOverhead;
        //    var initialSize = RoundUpTo8ByteAlignment(totalBytes);
        //    var builder = new FlatBufferBuilder(initialSize);
        //    var endpointOffset = builder.CreateString(contractIdentifier);
        //    var hwBlockOffset = builder.CreateString(serviceProviderIdentifier);
        //    var payloadOffset = DoStatePayload.CreateDoStatePayload(builder, endpointOffset, hwBlockOffset, value);
        //    DoStatePayload.FinishDoStatePayloadBuffer(builder, payloadOffset);

        //    return builder.SizedByteArray();
        //}

        //public static byte[] CreateAoStatePayload(string contractIdentifier, string serviceProviderIdentifier, double value)
        //{
        //    const int payloadTableOverhead = 24; // includes vtable + table header + padding + length-prefix metadata
        //    var totalBytes = contractIdentifier.Length + serviceProviderIdentifier.Length + EmbeddedDoubleSize + payloadTableOverhead;
        //    var initialSize = RoundUpTo8ByteAlignment(totalBytes);
        //    var builder = new FlatBufferBuilder(initialSize);
        //    var endpointOffset = builder.CreateString(contractIdentifier);
        //    var hwBlockOffset = builder.CreateString(serviceProviderIdentifier);
        //    var payloadOffset = AoStatePayload.CreateAoStatePayload(builder, endpointOffset, hwBlockOffset, value);
        //    AoStatePayload.FinishAoStatePayloadBuffer(builder, payloadOffset);

        //    return builder.SizedByteArray();
        //}

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

        ///*
        // * While FlatBuffers do not guarantee that the final serialized size will be naturally aligned, internal alignment rules can add extra padding.
        // * To avoid buffer growth, the size estimate is rounded up to the next multiple of 8.
        // *
        // * Benchmark results across realistic payloads showed:
        // *  - No buffer resizing required
        // *  - Exact fits in many cases
        // *  - Typically only 4–8 extra bytes allocated
        // *  - Worst observed overhead was 18 bytes
        // */
        //private static int RoundUpTo8ByteAlignment(int value)
        //{
        //    return (value + 7) & ~7;
        //}
    }
}