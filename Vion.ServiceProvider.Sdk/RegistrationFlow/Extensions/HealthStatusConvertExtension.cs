using Shared.Contracts.Events.MeshToCloud;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow.Extensions
{
    public static class HealthStatusConvertExtension
    {
        public static Shared.Contracts.FlatBuffers.System.Health.ConnectionStatus ToFlatBufferConnectionStatus(this ConnectionStatus connectionStatus)
        {
            return connectionStatus switch
            {
                ConnectionStatus.Online => Shared.Contracts.FlatBuffers.System.Health.ConnectionStatus.Online,
                ConnectionStatus.Offline => Shared.Contracts.FlatBuffers.System.Health.ConnectionStatus.Offline,
                _ => Shared.Contracts.FlatBuffers.System.Health.ConnectionStatus.Unknown,
            };
        }

        public static Shared.Contracts.FlatBuffers.System.Health.HealthStatus ToFlatBufferHealthStatus(this HealthStatus healthStatus)
        {
            return healthStatus switch
            {
                HealthStatus.Healthy => Shared.Contracts.FlatBuffers.System.Health.HealthStatus.Healthy,
                HealthStatus.Unhealthy => Shared.Contracts.FlatBuffers.System.Health.HealthStatus.Unhealthy,
                _ => Shared.Contracts.FlatBuffers.System.Health.HealthStatus.Unknown,
            };
        }
    }
}