using Vion.Contracts.Mqtt;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    internal static class ServiceProviderTopics
    {
        public static string GetSelectionTopic(string installationTopic, string serviceProviderIdentifier)
        {
            return $"{installationTopic}/{serviceProviderIdentifier}/serviceProvider/setup/selection";
        }

        public static string GetSetupSchemaTopic(string installationTopic, string serviceProviderIdentifier)
        {
            return $"{installationTopic}/{serviceProviderIdentifier}/serviceProvider/setup/schema";
        }

        public static string GetRegistrationRequestTopic(string registrationClientId)
        {
            return $"{Topics.ServiceProviderRegistrationRequest}/{registrationClientId}";
        }

        public static string GetRegistrationAcceptedTopic(string registrationClientId)
        {
            return $"{Topics.ServiceProviderRegistrationAccepted}/{registrationClientId}";
        }

        public static string GetRegistrationDeniedTopic(string registrationClientId)
        {
            return $"{Topics.ServiceProviderRegistrationDenied}/{registrationClientId}";
        }

        public static string GetContractTopicFilter(string installationTopic, string serviceProviderIdentifier, string serviceAndContractIdentifierPart)
        {
            return $"{installationTopic}/{serviceProviderIdentifier}/{serviceAndContractIdentifierPart}/#";
        }

        public static string GetTopicGetComponentHealth(string installationTopic, string serviceProviderIdentifier)
        {
            return $"{installationTopic}/{serviceProviderIdentifier}{Topics.ComponentHealthGet}";
        }

        public static string GetTopicComponentHealthState(string installationTopic, string serviceProviderIdentifier)
        {
            return $"{installationTopic}/{serviceProviderIdentifier}{Topics.ComponentHealthState}";
        }

        public static string GetServiceProviderDeclarationTopic(string installationTopic, string serviceProviderIdentifier)
        {
            return $"{installationTopic}/{serviceProviderIdentifier}{Topics.ServiceProviderDeclaration}";
        }

        public static string GetRestartTopic(string installationTopic, string serviceProviderIdentifier)
        {
            return $"{installationTopic}/{serviceProviderIdentifier}{Topics.ServiceProviderRestart}";
        }

        public static string LogLevelSetTopic(string installationTopic, string serviceProviderIdentifier)
        {
            return $"{installationTopic}/{serviceProviderIdentifier}{Topics.ServiceProviderLogLevelSet}";
        }

        public static string LogLevelStateTopic(string installationTopic, string serviceProviderIdentifier)
        {
            return $"{installationTopic}/{serviceProviderIdentifier}{Topics.ServiceProviderLogLevelState}";
        }
    }
}
