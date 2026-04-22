using System;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using Shared.Contracts.Events.MeshToCloud;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    /// <summary>
    /// Defines the contract for a service provider client that handles MQTT communication and registration flow.
    /// </summary>
    public interface IServiceProviderClient : IServiceProviderPublish
    {
        /// <summary>
        /// Event triggered when an MQTT application message is received and not handled by any registered handler.
        /// </summary>
        event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync; // todo return converted type

        /// <summary>
        /// Event triggered when the MQTT client successfully connects.
        /// </summary>
        event Func<MqttClientConnectedEventArgs, Task> ConnectedAsync;

        /// <summary>
        /// Event triggered when the MQTT client is in the process of connecting.
        /// </summary>
        event Func<MqttClientConnectingEventArgs, Task> ConnectingAsync;

        /// <summary>
        /// Event triggered when the MQTT client disconnects.
        /// </summary>
        event Func<MqttClientDisconnectedEventArgs, Task> DisconnectedAsync;

        /// <summary>
        /// Starts the service provider client and initiates the registration flow.
        /// </summary>
        /// <param name="appStoppingToken">Cancellation token for application shutdown.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StartAsync(CancellationToken? appStoppingToken);

        /// <summary>
        /// Publishes the health status of the service provider to the specified MQTT topic.
        /// </summary>
        /// <param name="topic">The MQTT topic to publish to.</param>
        /// <param name="connectionStatus">The connection status of the service provider.</param>
        /// <param name="healthStatus">The health status of the service provider.</param>
        /// <param name="since">The timestamp since when this status has been active.</param>
        /// <param name="client">The service provider client handler.</param>
        /// <param name="correlationData">Optional correlation data for the message.</param>
        /// <param name="retain">Whether the message should be retained by the broker.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task<MqttClientPublishResult> PublishHealthStatusAsync(string topic,
                                                               ConnectionStatus connectionStatus,
                                                               HealthStatus healthStatus,
                                                               DateTime since,
                                                               IServiceProviderClientHandler client,
                                                               byte[]? correlationData,
                                                               bool retain,
                                                               CancellationToken cancellationToken);

        /// <summary>
        /// Publishes the current log level state of the service provider.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PublishLogLevelStateAsync();
    }
}
