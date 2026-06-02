using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MQTTnet;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    internal sealed partial class MessageDispatcher : IMessageDispatcher
    {
        private readonly ILogger _logger;

        public MessageDispatcher(ILogger logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task DispatchAsync(MqttApplicationMessage message,
                                        IServiceProviderPublish publisher,
                                        IEnumerable<HandlerConfiguration> handlers,
                                        Guid correlationId,
                                        Func<Task>? fallback,
                                        CancellationToken cancellationToken)
        {
            var handled = false;
            List<Exception>? exceptions = null;
            foreach (var handler in handlers)
            {
                if (MqttTopicFilterComparer.Compare(message.Topic, handler.TopicFilter) != MqttTopicFilterCompareResult.IsMatch)
                {
                    continue;
                }

                handled = true;
                LogDispatchingMessageToHandler(correlationId, message.Topic);

                try
                {
                    await handler.Handler.Invoke(publisher, message, correlationId, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    exceptions ??= [];
                    exceptions.Add(exception);
                }
            }

            if (!handled)
            {
                if (fallback != null)
                {
                    await fallback.Invoke();
                }
                else
                {
                    LogNoHandlerFound(correlationId, message.Topic);
                }
            }

            switch (exceptions)
            {
                case { Count: 1 }:
                    throw exceptions[0];
                case { Count: > 1 }:
                    throw new AggregateException(exceptions);
            }
        }

        [LoggerMessage(Level = LogLevel.Debug, Message = "Dispatching message to handler (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogDispatchingMessageToHandler(Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Warning, Message = "No handler found for message (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogNoHandlerFound(Guid correlationId, string topic);
    }
}