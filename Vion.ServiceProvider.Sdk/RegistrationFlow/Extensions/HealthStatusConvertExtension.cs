using Vion.Contracts.FlatBuffers.System.Health;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow.Extensions
{
    /// <summary>
    ///     Extension methods for converting health status types to FlatBuffer format.
    /// </summary>
    public static class HealthStatusConvertExtension
    {
        /// <summary>
        ///     Converts a <see cref="Contracts.Events.MeshToCloud.ConnectionStatus" /> to FlatBuffer <see cref="Vion.Contracts.FlatBuffers.System.Health.ConnectionStatus" />.
        /// </summary>
        /// <param name="connectionStatus">The connection status to convert.</param>
        /// <returns>The FlatBuffer connection status.</returns>
        public static ConnectionStatus ToFlatBufferConnectionStatus(this Contracts.Events.MeshToCloud.ConnectionStatus connectionStatus)
        {
            return connectionStatus switch
            {
                Contracts.Events.MeshToCloud.ConnectionStatus.Online => ConnectionStatus.Online,
                Contracts.Events.MeshToCloud.ConnectionStatus.Offline => ConnectionStatus.Offline,
                _ => ConnectionStatus.Unknown,
            };
        }

        /// <summary>
        ///     Converts a <see cref="Contracts.Events.MeshToCloud.HealthStatus" /> to FlatBuffer <see cref="Vion.Contracts.FlatBuffers.System.Health.HealthStatus" />.
        /// </summary>
        /// <param name="healthStatus">The health status to convert.</param>
        /// <returns>The FlatBuffer health status.</returns>
        public static HealthStatus ToFlatBufferHealthStatus(this Contracts.Events.MeshToCloud.HealthStatus healthStatus)
        {
            return healthStatus switch
            {
                Contracts.Events.MeshToCloud.HealthStatus.Healthy => HealthStatus.Healthy,
                Contracts.Events.MeshToCloud.HealthStatus.Unhealthy => HealthStatus.Unhealthy,
                _ => HealthStatus.Unknown,
            };
        }
    }
}