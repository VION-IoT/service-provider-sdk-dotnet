using System.Threading;
using System.Threading.Tasks;
using MQTTnet;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    /// <summary>
    /// Defines the contract for publishing messages to MQTT topics.
    /// </summary>
    public interface IServiceProviderPublish
    {
        /// <summary>
        /// Publishes a message to the specified MQTT topic with schema information.
        /// </summary>
        /// <param name="topic">The MQTT topic to publish to.</param>
        /// <param name="schema">The schema name of the payload.</param>
        /// <param name="contentType">The content type of the payload.</param>
        /// <param name="payload">The message payload as byte array.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <param name="retain">Indicates whether the message should be retained by the broker.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task<MqttClientPublishResult> PublishAsync(string topic,
                                                   string schema,
                                                   string contentType,
                                                   byte[] payload,
                                                   CancellationToken cancellationToken,
                                                   bool retain = true);

        /// <summary>
        /// Publishes an MQTT application message.
        /// </summary>
        /// <param name="msg">The MQTT application message to publish.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage msg, CancellationToken cancellationToken);
    }
}
