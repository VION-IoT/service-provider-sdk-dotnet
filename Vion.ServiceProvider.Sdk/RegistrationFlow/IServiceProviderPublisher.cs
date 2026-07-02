using System;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Protocol;
using Vion.Contracts.Mqtt;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    /// <summary>
    ///     The strict publish surface. Every message carries a correlation ID and <c>published_at</c>; any non-empty
    ///     payload must declare its <c>schema</c> and content type. There is no raw <see cref="MqttApplicationMessage" />
    ///     overload — the SDK enforces the platform messaging contract on the way out.
    /// </summary>
    public interface IServiceProviderPublisher
    {
        /// <summary>
        ///     The installation topic root, assigned at registration acceptance. <c>null</c> before the service provider is
        ///     operational; non-null whenever this surface is handed to a message handler or the operational-ready callback.
        /// </summary>
        string? InstallationTopic { get; }

        /// <summary>
        ///     This service provider's identifier. <c>null</c> before the service provider is operational; non-null whenever
        ///     this surface is handed to a message handler or the operational-ready callback.
        /// </summary>
        string? ServiceProviderIdentifier { get; }

        /// <summary>
        ///     Publishes a message to the operational broker.
        /// </summary>
        /// <param name="topic">The MQTT topic to publish to.</param>
        /// <param name="correlationId">The correlation identifier for tracking the message flow.</param>
        /// <param name="cancellationToken">A token to cancel the publish.</param>
        /// <param name="contentType">The content type of the payload. Required when <paramref name="payload" /> is non-empty.</param>
        /// <param name="schema">
        ///     The <c>schema</c> user-property value for the payload. Required when <paramref name="payload" />
        ///     is non-empty.
        /// </param>
        /// <param name="payload">The message payload. May be empty for a payload-less signal.</param>
        /// <param name="qos">The MQTT quality-of-service level.</param>
        /// <param name="retain">Whether the broker should retain the message.</param>
        /// <returns><c>true</c> if the broker acknowledged the publish with a success reason code; <c>false</c> otherwise.</returns>
        /// <exception cref="ArgumentException">
        ///     Thrown when <paramref name="payload" /> is non-empty but <paramref name="schema" />
        ///     or <paramref name="contentType" /> is missing.
        /// </exception>
        /// <remarks>
        ///     Publishing does not throw on connection or transport failures — those are logged by the SDK and reported through
        ///     the <c>bool</c> result, so callers need not wrap publishes in try/catch. Passing a non-empty payload without a
        ///     schema or content type is a usage error and still throws <see cref="ArgumentException" />.
        /// </remarks>
        Task<bool> PublishMessageAsync(string topic,
                                       Guid correlationId,
                                       CancellationToken cancellationToken,
                                       string? contentType = null,
                                       string? schema = null,
                                       ReadOnlyMemory<byte> payload = default,
                                       MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
                                       bool retain = false);

        /// <summary>
        ///     Publishes a request to the operational broker, carrying the MQTT <c>ResponseTopic</c> a responder should reply
        ///     on. The SDK does not wait for or correlate the reply.
        /// </summary>
        /// <param name="topic">The MQTT topic to publish the request to.</param>
        /// <param name="responseTopic">The MQTT topic a responder should publish its reply to.</param>
        /// <param name="correlationId">The correlation identifier for tracking the message flow.</param>
        /// <param name="cancellationToken">A token to cancel the publish.</param>
        /// <param name="contentType">The content type of the payload. Required when <paramref name="payload" /> is non-empty.</param>
        /// <param name="schema">
        ///     The <c>schema</c> user-property value for the payload. Required when <paramref name="payload" />
        ///     is non-empty.
        /// </param>
        /// <param name="payload">The request payload. May be empty for a payload-less request.</param>
        /// <param name="qos">The MQTT quality-of-service level.</param>
        /// <param name="retain">Whether the broker should retain the message.</param>
        /// <returns><c>true</c> if the broker acknowledged the publish with a success reason code; <c>false</c> otherwise.</returns>
        /// <exception cref="ArgumentException">
        ///     Thrown when <paramref name="payload" /> is non-empty but <paramref name="schema" />
        ///     or <paramref name="contentType" /> is missing.
        /// </exception>
        /// <remarks>
        ///     Publishing does not throw on connection or transport failures — those are logged by the SDK and reported through
        ///     the <c>bool</c> result, so callers need not wrap publishes in try/catch. Passing a non-empty payload without a
        ///     schema or content type is a usage error and still throws <see cref="ArgumentException" />.
        /// </remarks>
        Task<bool> PublishRequestAsync(string topic,
                                       string responseTopic,
                                       Guid correlationId,
                                       CancellationToken cancellationToken,
                                       string? contentType = null,
                                       string? schema = null,
                                       ReadOnlyMemory<byte> payload = default,
                                       MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
                                       bool retain = false);

        /// <summary>
        ///     Publishes a request/response message, adding the <c>status</c> (and optional <c>error_code</c> /
        ///     <c>error_message</c>)
        ///     user properties alongside the contract metadata.
        /// </summary>
        /// <param name="topic">The MQTT topic to publish the response to.</param>
        /// <param name="status">The request status (<see cref="RequestStatus.Success" /> / <see cref="RequestStatus.Error" />).</param>
        /// <param name="correlationId">The correlation identifier for tracking the message flow.</param>
        /// <param name="cancellationToken">A token to cancel the publish.</param>
        /// <param name="contentType">The content type of the payload. Required when <paramref name="payload" /> is non-empty.</param>
        /// <param name="schema">
        ///     The <c>schema</c> user-property value for the payload. Required when <paramref name="payload" />
        ///     is non-empty.
        /// </param>
        /// <param name="payload">The response payload. May be empty for a status-only response.</param>
        /// <param name="errorCode">The optional <c>error_code</c> user-property value.</param>
        /// <param name="errorMessage">The optional <c>error_message</c> user-property value.</param>
        /// <param name="qos">The MQTT quality-of-service level.</param>
        /// <returns><c>true</c> if the broker acknowledged the publish with a success reason code; <c>false</c> otherwise.</returns>
        /// <exception cref="ArgumentException">
        ///     Thrown when <paramref name="payload" /> is non-empty but <paramref name="schema" />
        ///     or <paramref name="contentType" /> is missing.
        /// </exception>
        /// <remarks>
        ///     Publishing does not throw on connection or transport failures — those are logged by the SDK and reported through
        ///     the <c>bool</c> result, so callers need not wrap publishes in try/catch. Passing a non-empty payload without a
        ///     schema or content type is a usage error and still throws <see cref="ArgumentException" />.
        /// </remarks>
        Task<bool> PublishResponseAsync(string topic,
                                        RequestStatus status,
                                        Guid correlationId,
                                        CancellationToken cancellationToken,
                                        string? contentType = null,
                                        string? schema = null,
                                        ReadOnlyMemory<byte> payload = default,
                                        string? errorCode = null,
                                        string? errorMessage = null,
                                        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce);

        /// <summary>
        ///     Publishes the service provider's current component health to its retained component-health <c>state</c> topic — a
        ///     proactive push for when health changes and the SP does not want to wait to be polled.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the publish.</param>
        /// <returns><c>true</c> if the broker acknowledged the publish with a success reason code; <c>false</c> otherwise.</returns>
        /// <remarks>
        ///     The status published is whatever the health-check evaluator (registered via <c>WithHealthCheckEvaluator</c>)
        ///     reports at the moment of the
        ///     call, so the pushed state and the polled <c>get</c> response always agree; the connection status is always
        ///     <c>Online</c>. Before the service provider is operational there is no topic to publish to, so the call is a no-op
        ///     that returns <c>false</c>.
        ///     Publishing does not throw on connection or transport failures — those are logged by the SDK and reported through
        ///     the <c>bool</c> result, so callers need not wrap publishes in try/catch.
        /// </remarks>
        Task<bool> PublishHealthStateAsync(CancellationToken cancellationToken);
    }
}
