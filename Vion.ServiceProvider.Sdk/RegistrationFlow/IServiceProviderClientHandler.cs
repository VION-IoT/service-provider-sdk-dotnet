namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    /// <summary>
    /// Defines the contract for a service provider client handler with installation topic and identifier.
    /// </summary>
    public interface IServiceProviderClientHandler : IServiceProviderPublish
    {
        /// <summary>
        /// Gets the installation topic for this service provider.
        /// </summary>
        public string? InstallationTopic { get; }

        /// <summary>
        /// Gets the unique identifier of this service provider.
        /// </summary>
        public string? ServiceProviderIdentifier { get; }
    }
}