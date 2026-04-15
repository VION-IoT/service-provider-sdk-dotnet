using Shared.Contracts.Events.MeshToCloud;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow.Extensions
{
    /// <summary>
    /// Extension methods for converting health status types to FlatBuffer format.
    /// </summary>
    public static class HealthStatusConvertExtension
    {
        /// <summary>
        /// Converts a <see cref="ConnectionStatus"/> to FlatBuffer <see cref="Shared.Contracts.FlatBuffers.System.Health.ConnectionStatus"/>.
        /// </summary>
        /// <param name="connectionStatus">The connection status to convert.</param>
        /// <returns>The FlatBuffer connection status.</returns>
        public static Shared.Contracts.FlatBuffers.System.Health.ConnectionStatus ToFlatBufferConnectionStatus(this ConnectionStatus connectionStatus)
        {
            return connectionStatus switch
            {
                ConnectionStatus.Online => Shared.Contracts.FlatBuffers.System.Health.ConnectionStatus.Online,
                ConnectionStatus.Offline => Shared.Contracts.FlatBuffers.System.Health.ConnectionStatus.Offline,
                _ => Shared.Contracts.FlatBuffers.System.Health.ConnectionStatus.Unknown,
            };
        }

        /// <summary>
        /// Converts a <see cref="HealthStatus"/> to FlatBuffer <see cref="Shared.Contracts.FlatBuffers.System.Health.HealthStatus"/>.
        /// </summary>
        /// <param name="healthStatus">The health status to convert.</param>
        /// <returns>The FlatBuffer health status.</returns>
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