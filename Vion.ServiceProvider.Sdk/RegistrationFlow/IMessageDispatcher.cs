using System;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    internal interface IMessageDispatcher
    {
        /// <summary>
        ///     Dispatches a received message to every registered handler whose topic filter matches. Per-handler
        ///     exceptions are collected so the remaining handlers still run, then propagated to the caller (a single
        ///     exception is rethrown; multiple are wrapped in an <see cref="AggregateException" />).
        /// </summary>
        /// <param name="message">The received MQTT message.</param>
        /// <param name="publisher">The publish-only surface passed through to each invoked handler.</param>
        /// <param name="handlers">The registered handler configurations to match against.</param>
        /// <param name="correlationId">The correlation identifier for tracking the message flow.</param>
        /// <param name="cancellationToken">A token, cancelled on shutdown, passed through to each invoked handler.</param>
        /// <returns><c>true</c> if at least one handler's topic filter matched; otherwise <c>false</c>.</returns>
        Task<bool> DispatchAsync(MqttApplicationMessage message,
                                 IServiceProviderPublish publisher,
                                 HandlerConfiguration[] handlers,
                                 Guid correlationId,
                                 CancellationToken cancellationToken);
    }
}