using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
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
        public async Task<bool> DispatchAsync(MqttApplicationMessage message,
                                              IServiceProviderPublisher publisher,
                                              HandlerConfiguration[] handlers,
                                              Guid correlationId,
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

            switch (exceptions)
            {
                case { Count: 1 }: ExceptionDispatchInfo.Throw(exceptions[0]); break;
                case { Count: > 1 }: throw new AggregateException(exceptions);
            }

            return handled;
        }

        [LoggerMessage(Level = LogLevel.Debug, Message = "Dispatching message to handler (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogDispatchingMessageToHandler(Guid correlationId, string topic);
    }
}
