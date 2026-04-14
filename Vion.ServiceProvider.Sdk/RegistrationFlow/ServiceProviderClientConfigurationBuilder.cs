using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using MQTTnet;
using Shared.Contracts.Events.MeshToCloud;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    #region data classes

    public record OperationalData(MqttConnectionData ConnectionData, string InstallationTopic, string ClientId, string Username, string Password);

    public record MqttConnectionData(string ServiceProviderIdentifier, string Host, int Port);

    #endregion data classes

    public class ServiceProviderClientConfigurationBuilder
    {
        public ServiceProviderClientConfiguration Configuration { get; }

        public ServiceProviderClientConfigurationBuilder(MqttConnectionData connectionData, string secret)
        {
            Configuration = new ServiceProviderClientConfiguration { ConnectionData = connectionData, Secret = secret };
        }

        /// <summary>
        /// Provide SetupSchema and validate setup selection, e.g. by checking selected options
        /// </summary>
        /// <param name="setupSchemaPayload">The setup schema to send.</param>
        /// <param name="validationCallback">The setup selection validation func.</param>
        /// <returns>A builder for further builder.</returns>
        public SetupSelectionBuilder WithSetupSchema(Payloads.ServiceProviderSetupSchemaPayload setupSchemaPayload,
                                                     Func<Payloads.ServiceProviderSetupSelectionPayload, bool> validationCallback)
        {
            Configuration.SetupSelectionValidationCallback = validationCallback;
            Configuration.SetupSchemaPayload = setupSchemaPayload;
            return new SetupSelectionBuilder(Configuration);
        }

        public SetupSchemaBuilderHandlerRegistration WithDeclaration(Func<ServiceProviderDeclarationPayload> declarationCallback)
        {
            Configuration.DeclarationCallback = declarationCallback;
            return new SetupSchemaBuilderHandlerRegistration(Configuration);
        }
    }

    public class ServiceProviderClientConfiguration
    {
        public Payloads.ServiceProviderSetupSchemaPayload? SetupSchemaPayload { get; set; }

        public Func<Payloads.ServiceProviderSetupSelectionPayload, bool>? SetupSelectionValidationCallback { get; set; }

        public Func<Payloads.ServiceProviderSetupSelectionPayload, ServiceProviderDeclarationPayload>? DeclarationCallbackWithSetup { get; set; }

        public Func<ServiceProviderDeclarationPayload>? DeclarationCallback { get; set; }

        public required MqttConnectionData ConnectionData { get; init; }

        public required string Secret { get; init; }

        public Func<string, string, HandlerBuilder>? HandlerSetupCallback { get; set; }
    }

    public class SetupSelectionBuilder
    {
        private readonly ServiceProviderClientConfiguration _config;

        public SetupSelectionBuilder(ServiceProviderClientConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Build declaration based on setup selection, e.g. by including/excluding services or contracts
        /// </summary>
        /// <param name="declarationCallback">The declaration callback based on setup selection.</param>
        /// <returns>A builder for further configuration.</returns>
        public SetupSchemaBuilderHandlerRegistration WithDeclaration(Func<Payloads.ServiceProviderSetupSelectionPayload, ServiceProviderDeclarationPayload> declarationCallback)
        {
            _config.DeclarationCallbackWithSetup = declarationCallback;
            return new SetupSchemaBuilderHandlerRegistration(_config);
        }
    }

    public record HandlerConfiguration(
        string TopicPartToMatch,
        Func<IServiceProviderClientHandler, MqttApplicationMessageReceivedEventArgs, Task> Handler,
        bool IsContractTopic,
        string? TopicFilter = null)
    {
        public string? TopicFilter { get; set; } = TopicFilter;
    }

    public interface IHandlerBuilder
    {
        string InstallationTopic { get; }

        string ServiceProviderIdentifier { get; }

        void WithHandler(string topic, Func<IServiceProviderClientHandler, MqttApplicationMessageReceivedEventArgs, Task> handler);

        /// <summary>
        /// This adds a handler for the contract topic. It will subscribe following topic:
        /// {installationTopic}/{serviceProviderIdentifier}/{service}/{contract}/#
        /// </summary>
        /// <param name="service">The service part of the topic</param>
        /// <param name="contract">The contract part of the topic</param>
        /// <param name="handler">The handler to process the contract specific topics</param>

        // {installationTopic}/{serviceProviderIdentifier}/{service}/{contract}/{contract-specific-path}
        void WithContractHandler(string service, string contract, Func<IServiceProviderClientHandler, MqttApplicationMessageReceivedEventArgs, Task> handler);

        void WithHealthCheckEvaluator(Func<HealthStatus> healthCheckEvaluator);
    }

    public class HandlerBuilder : IHandlerBuilder
    {
        public List<HandlerConfiguration> ConfigHandlers { get; } = [];

        public Func<HealthStatus>? HealthCheckStatusProviderFunc { get; set; }

        public HandlerBuilder(string installationTopic, string serviceProviderIdentifier)
        {
            InstallationTopic = installationTopic;
            ServiceProviderIdentifier = serviceProviderIdentifier;
        }

        public string InstallationTopic { get; }

        public string ServiceProviderIdentifier { get; }

        public void WithHandler(string topic, Func<IServiceProviderClientHandler, MqttApplicationMessageReceivedEventArgs, Task> handler)
        {
            if (topic.Contains("+") || topic.Contains("#"))
            {
                // use something like MQTTnet.Extensions.TopicTemplate.MqttTopicTemplate to match generic topics with wildcards, e.g. for contract handlers
                // and update the OnApplicationMessageReceivedAsync to match incoming message topics to the registered handlers using the topic template matching
                throw new ValidationException("Invalid topic for handler, no wildcards allowed");
            }

            ConfigHandlers.Add(new HandlerConfiguration(topic, handler, false));
        }

        /// <summary>
        /// This adds a handler for the contract topic. It will subscribe following topic:
        /// {installationTopic}/{serviceProviderIdentifier}/{service}/{contract}/#
        /// </summary>
        /// <param name="service">The service part of the topic</param>
        /// <param name="contract">The contract part of the topic</param>
        /// <param name="handler">The handler to process the contract specific topics</param>

        // {installationTopic}/{serviceProviderIdentifier}/{service}/{contract}/{contract-specific-path}
        public void WithContractHandler(string service, string contract, Func<IServiceProviderClientHandler, MqttApplicationMessageReceivedEventArgs, Task> handler)
        {
            var topicPartToMatch = $"{service}/{contract}";
            ConfigHandlers.Add(new HandlerConfiguration(topicPartToMatch, handler, true));
        }

        public void WithHealthCheckEvaluator(Func<HealthStatus> healthCheckEvaluator)
        {
            HealthCheckStatusProviderFunc = healthCheckEvaluator;
        }
    }

    public class SetupSchemaBuilderHandlerRegistration
    {
        private readonly ServiceProviderClientConfiguration _config;

        public SetupSchemaBuilderHandlerRegistration(ServiceProviderClientConfiguration config)
        {
            _config = config;
        }

        public SetupSchemaBuilderFinish WithHandlers(Action<IHandlerBuilder> handlerSetupCallback)
        {
            _config.HandlerSetupCallback = (installationTopic, serviceProviderIdentifier) =>
                                           {
                                               var handlerBuilder = new HandlerBuilder(installationTopic, serviceProviderIdentifier);
                                               handlerSetupCallback(handlerBuilder);

                                               foreach (var handlerConfig in handlerBuilder.ConfigHandlers)
                                               {
                                                   if (handlerConfig.IsContractTopic)
                                                   {
                                                       var topicFilter = $"{installationTopic}/{serviceProviderIdentifier}/{handlerConfig.TopicPartToMatch}/#";
                                                       handlerConfig.TopicFilter = topicFilter;
                                                   }
                                                   else
                                                   {
                                                       handlerConfig.TopicFilter =
                                                           handlerConfig.TopicPartToMatch; // for non-contract handlers, the topic filter is just the topic to match
                                                   }
                                               }

                                               return handlerBuilder;
                                           };
            return new SetupSchemaBuilderFinish(_config);
        }
    }

    public class SetupSchemaBuilderFinish
    {
        private readonly ServiceProviderClientConfiguration _config;

        public SetupSchemaBuilderFinish(ServiceProviderClientConfiguration config)
        {
            _config = config;
        }

        public ServiceProviderClientConfiguration Build()
        {
            return _config;
        }
    }
}