using System;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    public interface IServiceProviderClient : IServiceProviderPublish
    {
        event Func<MqttApplicationMessageReceivedEventArgs, Task> ApplicationMessageReceivedAsync; // todo return converted type

        event Func<MqttClientConnectedEventArgs, Task> ConnectedAsync;

        event Func<MqttClientConnectingEventArgs, Task> ConnectingAsync;

        event Func<MqttClientDisconnectedEventArgs, Task> DisconnectedAsync;

        Task StartAsync(CancellationToken? appStoppingToken);
    }
}