using System;
using Vion.Contracts.Events.MeshToCloud;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    /// <summary>
    ///     The result of a service provider's health check evaluation, mapped onto the <c>component/health</c> response.
    /// </summary>
    /// <param name="Status">The current health status.</param>
    /// <param name="Reason">A human-readable reason for the current health state.</param>
    /// <param name="Since">The timestamp since when this status has been active.</param>
    public readonly record struct HealthCheckResult(HealthStatus Status, string? Reason = null, DateTime? Since = null);
}