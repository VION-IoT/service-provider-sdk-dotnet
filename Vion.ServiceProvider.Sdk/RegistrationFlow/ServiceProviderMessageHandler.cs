using System;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    /// <summary>
    ///     Handles a received MQTT message dispatched by the SDK.
    /// </summary>
    /// <param name="publisher">The publish-only surface, so the handler can emit a response.</param>
    /// <param name="message">The received MQTT message, unwrapped from its received-event args.</param>
    /// <param name="correlationId">The correlation identifier for tracking the message flow.</param>
    /// <param name="cancellationToken">A token that is cancelled when the service provider is shutting down.</param>
    /// <returns>A task representing the asynchronous handling of the message.</returns>
    public delegate Task ServiceProviderMessageHandler(IServiceProviderPublisher publisher,
                                                       MqttApplicationMessage message,
                                                       Guid correlationId,
                                                       CancellationToken cancellationToken);
}
