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
        ///     WriteOnly positions — a write-only field's whole value, or a struct field's write-only members
        ///     (per item for arrays of structs) — are replaced with the redacted sentinel before broadcast;
        ///     a null position stays null so a client can still tell an empty secret from a stored, hidden one.
        /// </remarks>
        Task PublishFieldAsync(IServiceProviderPublisher publisher, string serviceIdentifier, IServiceField field, JsonNode? value, CancellationToken cancellationToken);
    }
}
