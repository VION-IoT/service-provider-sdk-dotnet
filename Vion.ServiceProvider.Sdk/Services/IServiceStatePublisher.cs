using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Vion.ServiceProvider.Sdk.RegistrationFlow;

namespace Vion.ServiceProvider.Sdk.Services
{
    /// <summary>Publishes service-field values to their state topics.</summary>
    public interface IServiceStatePublisher
    {
        /// <summary>Publishes a field's current value to its state topic.</summary>
        /// <param name="publisher">The SDK publish target.</param>
        /// <param name="serviceIdentifier">The service the field belongs to.</param>
        /// <param name="field">The field whose value is being published.</param>
        /// <param name="value">The current value, or <c>null</c> if unset.</param>
        /// <param name="cancellationToken">A token to cancel the publish.</param>
        /// <exception cref="OperationCanceledException">The publish was canceled via <paramref name="cancellationToken" />.</exception>
        /// <remarks>
        ///     For write-only fields, a non-null <paramref name="value" /> is replaced with the redacted
        ///     sentinel before broadcast.
        /// </remarks>
        Task PublishFieldAsync(IServiceProviderPublish publisher, string serviceIdentifier, IServiceField field, JsonNode? value, CancellationToken cancellationToken);
    }
}
