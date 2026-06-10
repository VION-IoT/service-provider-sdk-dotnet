using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using Vion.ServiceProvider.Sdk.RegistrationFlow;

namespace Vion.ServiceProvider.Sdk.SystemControl
{
    /// <summary>
    ///     The default <c>restart</c> handler: stops the host so the container restart policy brings the service provider back
    ///     up.
    ///     Wired by <c>AddVionServiceProviderSdk</c>; service providers using the raw builder may also override the default
    ///     via <c>WithRestartCallback</c>.
    /// </summary>
    public sealed partial class RestartHandler
    {
        private readonly IHostApplicationLifetime _lifetime;

        private readonly ILogger<RestartHandler> _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="RestartHandler" /> class.
        /// </summary>
        /// <param name="lifetime">The host application lifetime used to stop the host.</param>
        /// <param name="logger">The logger.</param>
        public RestartHandler(IHostApplicationLifetime lifetime, ILogger<RestartHandler> logger)
        {
            _lifetime = lifetime;
            _logger = logger;
        }

        /// <summary>
        ///     Handles a <c>restart</c> message by stopping the host.
        /// </summary>
        /// <param name="publisher">The publish-only surface (unused — this handler does not respond).</param>
        /// <param name="message">The received MQTT restart message (unused — the command carries no payload).</param>
        /// <param name="correlationId">The correlation identifier for tracking the message flow.</param>
        /// <param name="cancellationToken">A token, canceled on shutdown (unused).</param>
        public Task HandleAsync(IServiceProviderPublisher publisher, MqttApplicationMessage message, Guid correlationId, CancellationToken cancellationToken)
        {
            LogShuttingDown(correlationId);

            // Run off the SDK message-dispatch task so we don't block the handler's await chain on shutdown.
            // The token is the app-stopping token, and StopApplication is what triggers that shutdown — forwarding it would
            // only skip the (idempotent) call once already stopping and leave this fire-and-forget task canceled, so it is intentionally not passed.
            // ReSharper disable once MethodSupportsCancellation
#pragma warning disable CA2016
            _ = Task.Run(_lifetime.StopApplication);
#pragma warning restore CA2016

            return Task.CompletedTask;
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "Received restart command — shutting down (CorrelationId={CorrelationId})")]
        private partial void LogShuttingDown(Guid correlationId);
    }
}
