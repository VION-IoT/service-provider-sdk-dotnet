using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Vion.ServiceProvider.Sdk.Infrastructure
{
    /// <summary>
    ///     Cross-cutting messaging logging shared by handlers across service providers — every handler logs message
    ///     receipt the same way. SDK-internal logs (receive boundary, publish, dispatch) live nested in their own classes.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static partial class MessagingLogging
    {
        /// <summary>Logs that a received message is being handled.</summary>
        /// <param name="logger">The logger.</param>
        /// <param name="correlationId">The correlation identifier for tracking the message flow.</param>
        /// <param name="topic">The MQTT topic the message arrived on.</param>
        [LoggerMessage(Level = LogLevel.Debug, Message = "Handling message (CorrelationId={CorrelationId}, Topic={Topic})")]
        public static partial void LogHandlingMessage(this ILogger logger, Guid correlationId, string topic);
    }
}