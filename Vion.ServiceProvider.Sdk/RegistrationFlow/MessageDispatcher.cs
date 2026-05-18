using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MQTTnet;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    internal sealed class MessageDispatcher : IMessageDispatcher
    {
        private readonly ILogger _logger;

        public MessageDispatcher(ILogger logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task DispatchAsync(MqttApplicationMessageReceivedEventArgs args,
                                        IServiceProviderClientHandler client,
                                        IEnumerable<HandlerConfiguration> handlers,
                                        Func<MqttApplicationMessageReceivedEventArgs, Task>? fallback)
        {
            var matchingHandlers = handlers.Where(h => MqttTopicFilterComparer.Compare(args.ApplicationMessage.Topic, h.TopicFilter) == MqttTopicFilterCompareResult.IsMatch)
                                           .ToList();
            if (matchingHandlers.Count == 0)
            {
                await (fallback?.Invoke(args) ?? Task.CompletedTask);
                return;
            }

            foreach (var handler in matchingHandlers)
            {
                try
                {
                    await handler.Handler.Invoke(client, args);
                }
                catch (Exception e)
                {
                    _logger.LogError(e,
                                     "Error occurred while handling message on topic '{Topic}' in handler registered for '{TopicPart}'",
                                     args.ApplicationMessage.Topic,
                                     handler.TopicPartToMatch);
                }
            }
        }
    }
}