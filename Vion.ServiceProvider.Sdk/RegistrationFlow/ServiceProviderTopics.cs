using Vion.Contracts.Mqtt;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    internal static class ServiceProviderTopics
    {
        public static string GetSelectionTopic(string installationTopic, string serviceProviderIdentifier)
        {
            return $"{installationTopic}{serviceProviderIdentifier}/serviceProvider/setup/selection";
        }

        public static string GetSetupSchemaTopic(string installationTopic, string serviceProviderIdentifier)
        {
            return $"{installationTopic}{serviceProviderIdentifier}/serviceProvider/setup/schema";
        }

        public static string GetRegistrationAcceptedTopic(string secret)
        {
            return $"{Topics.ServiceProviderRegistrationAccepted}/{secret}";
        }

        public static string GetRegistrationDeniedTopic(string secret)
        {
            return $"{Topics.ServiceProviderRegistrationDenied}/{secret}";
        }

        public static string GetContractTopicFilter(string installationTopic, string serviceProviderIdentifier, string serviceAndContractIdentifierPart)
        {
            return $"{installationTopic}{serviceProviderIdentifier}/{serviceAndContractIdentifierPart}/#";
        }
    }
}
