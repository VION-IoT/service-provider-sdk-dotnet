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
        /// <returns><c>true</c> if the message was handed to a connected broker; <c>false</c> if it could not be published.</returns>
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
        /// <returns><c>true</c> if the message was handed to a connected broker; <c>false</c> if it could not be published.</returns>
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
    }
}
