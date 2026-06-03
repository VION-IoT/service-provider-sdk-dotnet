using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Vion.ServiceProvider.Sdk.Services
{
    /// <summary>Owns a service's persisted state.</summary>
    /// <remarks>
    ///     Updates are serialized internally, so the persisted state stays consistent even under concurrent callers.
    ///     Notification order, however, is only guaranteed for a single writer: with concurrent writers, <see cref="StateChanged" /> subscribers may be notified
    ///     out of order and must not depend on it.
    /// </remarks>
    /// <typeparam name="TService">The service type whose state is managed.</typeparam>
    public interface IServiceStateStore<TService>
    {
        /// <summary>Raised after a successful update or initial load.</summary>
        event Func<TService, Task>? StateChanged;

        /// <summary>Loads any persisted state and raises <see cref="StateChanged" />.</summary>
        /// <param name="cancellationToken">A token to cancel the load.</param>
        /// <returns>The loaded snapshot, or the default if no state has been persisted.</returns>
        /// <exception cref="OperationCanceledException">The load was canceled via <paramref name="cancellationToken" />.</exception>
        Task<TService> InitializeAsync(CancellationToken cancellationToken);

        /// <summary>Applies a single field update, persists, and raises <see cref="StateChanged" />.</summary>
        /// <param name="field">The field identifier to update.</param>
        /// <param name="value">The new value, or <c>null</c> to clear the field.</param>
        /// <param name="cancellationToken">A token to cancel the update.</param>
        /// <returns>The new snapshot after the field is applied.</returns>
        /// <exception cref="ArgumentException">No field with the given identifier exists in the schema.</exception>
        /// <exception cref="InvalidOperationException">The store has not been initialized.</exception>
        /// <exception cref="OperationCanceledException">The update was canceled via <paramref name="cancellationToken" />.</exception>
        Task<TService> UpdateAsync(string field, JsonNode? value, CancellationToken cancellationToken);

        /// <summary>Returns the current in-memory state snapshot.</summary>
        /// <param name="cancellationToken">A token to cancel the read.</param>
        /// <returns>The current snapshot.</returns>
        /// <exception cref="InvalidOperationException">The store has not been initialized.</exception>
        /// <exception cref="OperationCanceledException">The read was canceled via <paramref name="cancellationToken" />.</exception>
        Task<TService> GetCurrentAsync(CancellationToken cancellationToken);
    }
}