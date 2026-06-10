using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MQTTnet;
using Vion.ServiceProvider.Sdk.Infrastructure;
using Vion.ServiceProvider.Sdk.JsonSerializationContexts;
using Vion.ServiceProvider.Sdk.RegistrationFlow;

namespace Vion.ServiceProvider.Sdk.SystemControl
{
    /// <summary>
    ///     The default <c>logLevel/set</c> handler: parses the incoming payload and updates
    ///     <see cref="LogLevelManager.CurrentLevel" />.
    ///     Wired by <c>AddVionServiceProviderSdk</c>; service providers using the raw builder may also override the default via
    ///     <c>WithLogLevelChangeCallback</c>.
    /// </summary>
    public sealed partial class SetLogLevelHandler
    {
        private readonly ILogger<SetLogLevelHandler> _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SetLogLevelHandler" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public SetLogLevelHandler(ILogger<SetLogLevelHandler> logger)
        {
            _logger = logger;
        }

        /// <summary>
        ///     Handles a <c>logLevel/set</c> message by updating <see cref="LogLevelManager.CurrentLevel" />.
        /// </summary>
        /// <param name="publisher">The publish-only surface (unused — this handler does not respond).</param>
        /// <param name="message">The received MQTT message carrying a <c>SetLogLevelPayload</c>.</param>
        /// <param name="correlationId">The correlation identifier for tracking the message flow.</param>
        /// <param name="cancellationToken">A token, canceled on shutdown (unused).</param>
        public Task HandleAsync(IServiceProviderPublisher publisher, MqttApplicationMessage message, Guid correlationId, CancellationToken cancellationToken)
        {
            var payload = message.GetJsonPayload(ServiceProviderJsonContext.Default.SetLogLevelPayload);
            LogSettingLogLevel(LogLevelManager.CurrentLevel, payload.LogLevel, correlationId);
            LogLevelManager.CurrentLevel = payload.LogLevel;

            return Task.CompletedTask;
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "Setting log level (Current={Current}, New={New}, CorrelationId={CorrelationId})")]
        private partial void LogSettingLogLevel(LogLevel current, LogLevel @new, Guid correlationId);
    }
}
