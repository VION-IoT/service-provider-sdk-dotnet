using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MQTTnet;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    internal interface IMessageDispatcher
    {
        /// <summary>
        ///     Dispatches an MQTT message to each registered handler whose topic filter matches, or invokes the fallback when none match.
        /// </summary>
        /// <param name="args">The MQTT application message received event arguments.</param>
        /// <param name="client">The client context passed through to each invoked handler.</param>
        /// <param name="handlers">The registered handler configurations to match against.</param>
        /// <param name="fallback">Callback invoked when no handler topic filter matches.</param>
        /// <returns>A task representing the asynchronous dispatch operation. Per-handler exceptions are logged and do not abort dispatch to the remaining handlers.</returns>
        Task DispatchAsync(MqttApplicationMessageReceivedEventArgs args,
                           IServiceProviderClientHandler client,
                           IEnumerable<HandlerConfiguration> handlers,
                           Func<MqttApplicationMessageReceivedEventArgs, Task>? fallback);
    }
}