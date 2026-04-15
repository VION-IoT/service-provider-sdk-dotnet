using System;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;

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
    }
}