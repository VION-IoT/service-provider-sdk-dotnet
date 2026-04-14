namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    public interface IServiceProviderClientHandler : IServiceProviderPublish
    {
        public string? InstallationTopic { get; }

        public string? ServiceProviderIdentifier { get; }
    }
}