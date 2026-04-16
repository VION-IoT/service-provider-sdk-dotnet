using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using MQTTnet;
using Shared.Contracts.Events.MeshToCloud;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    #region data classes

    /// <summary>
    /// Represents operational data for the service provider including connection and authentication details.
    /// </summary>
    public record OperationalData(MqttConnectionData ConnectionData, string InstallationTopic, string ClientId, string Username, string Password);

    /// <summary>
    /// Represents MQTT connection data including service provider identifier and broker address.
    /// </summary>
    public record MqttConnectionData(string ServiceProviderIdentifier, string Host, int Port);

    #endregion data classes

    /// <summary>
    /// Builder class for constructing service provider client configuration.
    /// </summary>
    public class ServiceProviderClientConfigurationBuilder
    {
        /// <summary>
        /// Gets the configuration being built.
        /// </summary>
        public ServiceProviderClientConfiguration Configuration { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceProviderClientConfigurationBuilder" /> class.
        /// </summary>
        /// <param name="connectionData">The MQTT connection data.</param>
        /// <param name="secret">The secret for authentication.</param>
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
                                                     Func<Payloads.ServiceProviderSetupSelectionPayload, Payloads.ServiceProviderSetupSchemaPayload, bool> validationCallback)
        {
            Configuration.SetupSelectionValidationCallback = validationCallback;
            Configuration.SetupSchemaPayload = setupSchemaPayload;
            return new SetupSelectionBuilder(Configuration);
        }

        /// <summary>
        /// Sets the declaration callback for the service provider.
        /// </summary>
        /// <param name="declarationCallback">The callback function that provides the service provider declaration.</param>
        /// <returns>A builder for handler registration.</returns>
        public SetupSchemaBuilderHandlerRegistration WithDeclaration(Func<ServiceProviderDeclarationPayload> declarationCallback)
        {
            Configuration.DeclarationCallback = declarationCallback;
            return new SetupSchemaBuilderHandlerRegistration(Configuration);
        }
    }

    /// <summary>
    /// Configuration settings for the service provider client.
    /// </summary>
    public class ServiceProviderClientConfiguration
    {
        /// <summary>
        /// Gets or sets the setup schema payload defining the configuration fields.
        /// </summary>
        public Payloads.ServiceProviderSetupSchemaPayload? SetupSchemaPayload { get; set; }

        /// <summary>
        /// Gets or sets the payload containing the selected service provider setup information.
        /// </summary>
        public Payloads.ServiceProviderSetupSelectionPayload? SetupSelectionPayload { get; set; }

        /// <summary>
        /// Gets or sets the declaration payload for the service provider, which can be used when no setup schema is provided.
        /// </summary>
        public ServiceProviderDeclarationPayload? DeclarationPayload { get; set; }

        /// <summary>
        /// Gets or sets the callback for validating the setup selection.
        /// </summary>
        public Func<Payloads.ServiceProviderSetupSelectionPayload, Payloads.ServiceProviderSetupSchemaPayload, bool>? SetupSelectionValidationCallback { get; set; }

        /// <summary>
        /// Gets or sets the callback for building the declaration based on setup selection.
        /// </summary>
        public Func<Payloads.ServiceProviderSetupSelectionPayload, Payloads.ServiceProviderSetupSchemaPayload, ServiceProviderDeclarationPayload>? DeclarationCallbackWithSetup
        {
            get;

            set;
        }

        /// <summary>
        /// Gets or sets the callback for building the declaration without setup.
        /// </summary>
        public Func<ServiceProviderDeclarationPayload>? DeclarationCallback { get; set; }

        /// <summary>
        /// Gets or initializes the MQTT connection data.
        /// </summary>
        public required MqttConnectionData ConnectionData { get; init; }

        /// <summary>
        /// Gets or initializes the authentication secret.
        /// </summary>
        public required string Secret { get; init; }

        /// <summary>
        /// Gets or sets the callback for setting up message handlers.
        /// </summary>
        public Func<string, string, ServiceProviderDeclarationPayload, HandlerBuilder>? HandlerSetupCallback { get; set; }
    }

    /// <summary>
    /// Builder class for configuring setup selection declaration.
    /// </summary>
    public class SetupSelectionBuilder
    {
        private readonly ServiceProviderClientConfiguration _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="SetupSelectionBuilder" /> class.
        /// </summary>
        /// <param name="config">The configuration being built.</param>
        public SetupSelectionBuilder(ServiceProviderClientConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Build declaration based on setup selection, e.g. by including/excluding services or contracts
        /// </summary>
        /// <param name="declarationCallback">The declaration callback based on setup selection.</param>
        /// <returns>A builder for further configuration.</returns>
        public SetupSchemaBuilderHandlerRegistration WithDeclaration(
            Func<Payloads.ServiceProviderSetupSelectionPayload, Payloads.ServiceProviderSetupSchemaPayload, ServiceProviderDeclarationPayload> declarationCallback)
        {
            _config.DeclarationCallbackWithSetup = declarationCallback;
            return new SetupSchemaBuilderHandlerRegistration(_config);
        }
    }

    /// <summary>
    /// Configuration for a message handler including topic matching and handler function.
    /// </summary>
    public record HandlerConfiguration(
        string TopicPartToMatch,
        Func<IServiceProviderClientHandler, MqttApplicationMessageReceivedEventArgs, Task> Handler,
        bool IsContractTopic,
        string? TopicFilter = null)
    {
        /// <summary>
        /// Gets or sets the MQTT topic filter for subscription.
        /// </summary>
        public string? TopicFilter { get; set; } = TopicFilter;
    }

    /// <summary>
    /// Interface for building and configuring message handlers for the service provider.
    /// </summary>
    public interface IHandlerBuilder
    {
        /// <summary>
        /// Gets the installation topic for this handler.
        /// </summary>
        string InstallationTopic { get; }

        /// <summary>
        /// Gets the service provider identifier.
        /// </summary>
        string ServiceProviderIdentifier { get; }

        /// <summary>
        /// Gets the declaration payload for the service provider, which can be used to access declaration information when
        /// configuring handlers.
        /// </summary>
        ServiceProviderDeclarationPayload DeclarationPayload { get; }

        /// <summary>
        /// Adds a handler for a specific topic.
        /// </summary>
        /// <param name="topic">The topic to handle (no wildcards allowed).</param>
        /// <param name="handler">The handler function to process messages.</param>
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

        /// <summary>
        /// Sets the health check evaluator function for monitoring service health.
        /// </summary>
        /// <param name="healthCheckEvaluator">The function that evaluates and returns the current health status.</param>
        void WithHealthCheckEvaluator(Func<HealthStatus> healthCheckEvaluator);
    }

    /// <summary>
    /// Implementation of <see cref="IHandlerBuilder" /> for building message handlers.
    /// </summary>
    public class HandlerBuilder : IHandlerBuilder
    {
        /// <summary>
        /// Gets the list of configured handlers.
        /// </summary>
        public List<HandlerConfiguration> ConfigHandlers { get; } = [];

        /// <summary>
        /// Gets or sets the health check status provider function.
        /// </summary>
        public Func<HealthStatus>? HealthCheckStatusProviderFunc { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HandlerBuilder" /> class.
        /// </summary>
        /// <param name="installationTopic">The installation topic.(ends with '/')</param>
        /// <param name="serviceProviderIdentifier">The service provider identifier.</param>
        /// <param name="declarationPayload"></param>
        public HandlerBuilder(string installationTopic, string serviceProviderIdentifier, ServiceProviderDeclarationPayload declarationPayload)
        {
            InstallationTopic = installationTopic;
            ServiceProviderIdentifier = serviceProviderIdentifier;
            DeclarationPayload = declarationPayload;
        }

        /// <summary>
        /// Gets the declaration payload for the service provider, which can be used to access declaration information when
        /// configuring handlers.
        /// </summary>
        public ServiceProviderDeclarationPayload DeclarationPayload { get; }

        /// <inheritdoc />
        public string InstallationTopic { get; }

        /// <inheritdoc />
        public string ServiceProviderIdentifier { get; }

        /// <inheritdoc />
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

        /// <summary>
        /// This allows to set a health check evaluator function that can be used to monitor the health status of the service
        /// provider. The function should return a HealthStatus indicating the current health state.
        /// </summary>
        /// <param name="healthCheckEvaluator">The function to evaluate the health status.</param>
        public void WithHealthCheckEvaluator(Func<HealthStatus> healthCheckEvaluator)
        {
            HealthCheckStatusProviderFunc = healthCheckEvaluator;
        }
    }

    /// <summary>
    /// Builder class for registering message handlers after setup schema or declaration configuration.
    /// </summary>
    public class SetupSchemaBuilderHandlerRegistration
    {
        private readonly ServiceProviderClientConfiguration _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="SetupSchemaBuilderHandlerRegistration" /> class.
        /// </summary>
        /// <param name="config">The configuration being built.</param>
        public SetupSchemaBuilderHandlerRegistration(ServiceProviderClientConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Configures the message handlers for the service provider.
        /// </summary>
        /// <param name="handlerSetupCallback">The callback action to configure handlers.</param>
        /// <returns>A builder for completing the configuration.</returns>
        public SetupSchemaBuilderFinish WithHandlers(Action<IHandlerBuilder> handlerSetupCallback)
        {
            _config.HandlerSetupCallback = (installationTopic, serviceProviderIdentifier, declarationPayload) =>
                                           {
                                               var handlerBuilder = new HandlerBuilder(installationTopic, serviceProviderIdentifier, declarationPayload);
                                               handlerSetupCallback(handlerBuilder);

                                               foreach (var handlerConfig in handlerBuilder.ConfigHandlers)
                                               {
                                                   if (handlerConfig.IsContractTopic)
                                                   {
                                                       var topicFilter = ServiceProviderTopics.GetContractTopicFilter(installationTopic,
                                                                                                                      serviceProviderIdentifier,
                                                                                                                      handlerConfig.TopicPartToMatch);
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

    /// <summary>
    /// Final builder class for completing the service provider client configuration.
    /// </summary>
    public class SetupSchemaBuilderFinish
    {
        private readonly ServiceProviderClientConfiguration _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="SetupSchemaBuilderFinish" /> class.
        /// </summary>
        /// <param name="config">The configuration being built.</param>
        public SetupSchemaBuilderFinish(ServiceProviderClientConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        /// Builds and returns the completed service provider client configuration.
        /// </summary>
        /// <returns>The configured <see cref="ServiceProviderClientConfiguration" />.</returns>
        public ServiceProviderClientConfiguration Build()
        {
            return _config;
        }
    }
}
