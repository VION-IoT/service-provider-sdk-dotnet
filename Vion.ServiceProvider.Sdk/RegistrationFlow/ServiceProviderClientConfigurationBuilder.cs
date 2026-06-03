using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Vion.Contracts.Events.MeshToCloud;
using Vion.ServiceProvider.Sdk.Setup;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    #region data classes

    /// <summary>
    ///     Represents operational data for the service provider including connection and authentication details.
    /// </summary>
    public record OperationalData(MqttConnectionData ConnectionData, string InstallationTopic, string ClientId, string Username, string Password);

    /// <summary>
    ///     Represents MQTT connection data including service provider identifier and broker address.
    /// </summary>
    public record MqttConnectionData(string ServiceProviderIdentifier, string Host, int Port);

    #endregion data classes

    /// <summary>
    ///     Builder class for constructing service provider client configuration.
    /// </summary>

    // ReSharper disable once UnusedType.Global used the code using this sdk
    public class ServiceProviderClientConfigurationBuilder
    {
        /// <summary>
        ///     Gets the configuration being built.
        /// </summary>
        private ServiceProviderClientConfiguration Configuration { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ServiceProviderClientConfigurationBuilder" /> class.
        /// </summary>
        /// <param name="connectionData">The MQTT connection data.</param>
        /// <param name="secret">The secret for authentication.</param>
        public ServiceProviderClientConfigurationBuilder(MqttConnectionData connectionData, string secret)
        {
            Configuration = new ServiceProviderClientConfiguration { ConnectionData = connectionData, Secret = secret };
        }

        /// <summary>
        ///     Provide SetupSchema and validate setup selection, e.g. by checking selected options
        /// </summary>
        /// <remarks>
        ///     The validation callback runs on every successful (re)connection — the SDK re-runs the setup-schema exchange
        ///     whenever the operational connection is (re)established. Keep it cheap and side-effect-free.
        /// </remarks>
        /// <param name="setupSchemaPayload">The setup schema to send.</param>
        /// <param name="validationCallback">The setup selection validation func.</param>
        /// <returns>A builder for further builder.</returns>
        public SetupSelectionBuilder WithSetupSchema(ServiceProviderSetupSchemaPayload setupSchemaPayload,
                                                     Func<ServiceProviderSetupSelectionPayload, ServiceProviderSetupSchemaPayload, bool> validationCallback)
        {
            Configuration.SetupSelectionValidationCallback = validationCallback;
            Configuration.SetupSchemaPayload = setupSchemaPayload;
            return new SetupSelectionBuilder(Configuration);
        }

        /// <summary>
        ///     Sets the declaration callback for the service provider.
        /// </summary>
        /// <remarks>
        ///     The callback runs on every successful (re)connection — the SDK rebuilds and resends the declaration whenever the
        ///     operational connection is (re)established. Keep it cheap and idempotent.
        /// </remarks>
        /// <param name="declarationCallback">The callback function that provides the service provider declaration.</param>
        /// <returns>A builder for handler registration and optional lifecycle overrides.</returns>
        public ServiceProviderClientBuilder WithDeclaration(Func<ServiceProviderDeclarationPayload> declarationCallback)
        {
            Configuration.DeclarationCallback = declarationCallback;
            return new ServiceProviderClientBuilder(Configuration);
        }
    }

    /// <summary>
    ///     Configuration settings for the service provider client.
    /// </summary>
    public class ServiceProviderClientConfiguration
    {
        /// <summary>
        ///     Gets or sets the setup schema payload defining the configuration fields.
        /// </summary>
        public ServiceProviderSetupSchemaPayload? SetupSchemaPayload { get; set; }

        /// <summary>
        ///     Gets or sets the payload containing the selected service provider setup information.
        /// </summary>
        public ServiceProviderSetupSelectionPayload? SetupSelectionPayload { get; set; }

        /// <summary>
        ///     Gets or sets the declaration payload for the service provider, which can be used when no setup schema is provided.
        /// </summary>
        public ServiceProviderDeclarationPayload? DeclarationPayload { get; set; }

        /// <summary>
        ///     Gets or sets the callback for validating the setup selection.
        /// </summary>
        public Func<ServiceProviderSetupSelectionPayload, ServiceProviderSetupSchemaPayload, bool>? SetupSelectionValidationCallback { get; set; }

        /// <summary>
        ///     Gets or sets the callback for building the declaration based on setup selection.
        /// </summary>
        public Func<ServiceProviderSetupSelectionPayload, ServiceProviderSetupSchemaPayload, ServiceProviderDeclarationPayload>? DeclarationCallbackWithSetup { get; set; }

        /// <summary>
        ///     Gets or sets the callback for building the declaration without setup.
        /// </summary>
        public Func<ServiceProviderDeclarationPayload>? DeclarationCallback { get; set; }

        /// <summary>
        ///     Gets or initializes the MQTT connection data.
        /// </summary>
        public required MqttConnectionData ConnectionData { get; init; }

        /// <summary>
        ///     Gets or initializes the authentication secret.
        /// </summary>
        public required string Secret { get; init; }

        /// <summary>
        ///     Gets or sets the callback for setting up message handlers.
        /// </summary>
        public Func<string, string, ServiceProviderDeclarationPayload, HandlerBuilder>? HandlerSetupCallback { get; set; }

        /// <summary>
        ///     Gets or sets the optional override for the <c>restart</c> handler. When <c>null</c>, the SDK uses its default
        ///     restart handler.
        /// </summary>
        public ServiceProviderMessageHandler? OnRestartCallback { get; set; }

        /// <summary>
        ///     Gets or sets the optional override for the <c>logLevel/set</c> handler. When <c>null</c>, the SDK uses its default
        ///     handler, which sets <see cref="SystemControl.LogLevelManager.CurrentLevel" />.
        /// </summary>
        public ServiceProviderMessageHandler? OnLogLevelChangeCallback { get; set; }

        /// <summary>
        ///     Gets or sets the callback for providing the current log level. Defaults to
        ///     <see cref="SystemControl.LogLevelManager.CurrentLevel" /> when not supplied.
        /// </summary>
        public Func<LogLevel>? CurrentLogLevelProviderCallback { get; set; }

        /// <summary>
        ///     Gets or sets the callback invoked once the service provider is fully operational — registered, declared,
        ///     subscribed, and with initial SDK state published. <c>null</c> when not configured.
        /// </summary>
        public Func<IServiceProviderPublish, CancellationToken, Task>? OnOperationalReadyCallback { get; set; }
    }

    /// <summary>
    ///     Builder class for configuring setup selection declaration.
    /// </summary>
    public class SetupSelectionBuilder
    {
        private readonly ServiceProviderClientConfiguration _config;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SetupSelectionBuilder" /> class.
        /// </summary>
        /// <param name="config">The configuration being built.</param>
        public SetupSelectionBuilder(ServiceProviderClientConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        ///     Build declaration based on setup selection, e.g. by including/excluding services or contracts
        /// </summary>
        /// <remarks>
        ///     The callback runs on every successful (re)connection — the SDK rebuilds and resends the declaration whenever the
        ///     operational connection is (re)established. Keep it cheap and idempotent.
        /// </remarks>
        /// <param name="declarationCallback">The declaration callback based on setup selection.</param>
        /// <returns>A builder for handler registration and optional lifecycle overrides.</returns>
        public ServiceProviderClientBuilder WithDeclaration(
            Func<ServiceProviderSetupSelectionPayload, ServiceProviderSetupSchemaPayload, ServiceProviderDeclarationPayload> declarationCallback)
        {
            _config.DeclarationCallbackWithSetup = declarationCallback;
            return new ServiceProviderClientBuilder(_config);
        }
    }

    /// <summary>
    ///     Configuration for a message handler including topic matching and handler function.
    /// </summary>
    public record HandlerConfiguration(string TopicPartToMatch, ServiceProviderMessageHandler Handler, bool IsContractTopic, string? TopicFilter = null)
    {
        /// <summary>
        ///     Gets or sets the MQTT topic filter for subscription.
        /// </summary>
        public string? TopicFilter { get; set; } = TopicFilter;
    }

    /// <summary>
    ///     Interface for building and configuring message handlers for the service provider.
    /// </summary>
    public interface IHandlerBuilder
    {
        /// <summary>
        ///     Gets the installation topic for this handler.
        /// </summary>
        string InstallationTopic { get; }

        /// <summary>
        ///     Gets the service provider identifier.
        /// </summary>
        string ServiceProviderIdentifier { get; }

        /// <summary>
        ///     Gets the declaration payload for the service provider, which can be used to access declaration information when
        ///     configuring handlers.
        /// </summary>
        ServiceProviderDeclarationPayload DeclarationPayload { get; }

        /// <summary>
        ///     Adds a handler for a specific topic.
        /// </summary>
        /// <param name="topic">
        ///     The MQTT topic filter to handle. Wildcards <c>+</c> (single level) and <c>#</c> (multi level) are permitted; matching uses MQTT's standard topic-filter
        ///     semantics.
        /// </param>
        /// <param name="handler">The handler function to process messages.</param>
        void WithHandler(string topic, ServiceProviderMessageHandler handler);

        /// <summary>
        ///     This adds a handler for the contract topic. It will subscribe following topic:
        ///     {installationTopic}/{serviceProviderIdentifier}/{service}/{contract}/#
        /// </summary>
        /// <param name="service">The service part of the topic</param>
        /// <param name="contract">The contract part of the topic</param>
        /// <param name="handler">The handler to process the contract specific topics</param>

        // {installationTopic}/{serviceProviderIdentifier}/{service}/{contract}/{contract-specific-path}
        void WithContractHandler(string service, string contract, ServiceProviderMessageHandler handler);

        /// <summary>
        ///     Sets the health check evaluator function for monitoring service health.
        /// </summary>
        /// <param name="healthCheckEvaluator">The function that evaluates and returns the current health status.</param>
        void WithHealthCheckEvaluator(Func<HealthStatus> healthCheckEvaluator);
    }

    /// <summary>
    ///     Implementation of <see cref="IHandlerBuilder" /> for building message handlers.
    /// </summary>
    public class HandlerBuilder : IHandlerBuilder
    {
        /// <summary>
        ///     Gets the list of configured handlers.
        /// </summary>
        public List<HandlerConfiguration> ConfigHandlers { get; } = [];

        /// <summary>
        ///     Gets or sets the health check status provider function.
        /// </summary>
        public Func<HealthStatus>? HealthCheckStatusProviderFunc { get; private set; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="HandlerBuilder" /> class.
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
        ///     Gets the declaration payload for the service provider, which can be used to access declaration information when
        ///     configuring handlers.
        /// </summary>
        public ServiceProviderDeclarationPayload DeclarationPayload { get; }

        /// <inheritdoc />
        public string InstallationTopic { get; }

        /// <inheritdoc />
        public string ServiceProviderIdentifier { get; }

        /// <inheritdoc />
        public void WithHandler(string topic, ServiceProviderMessageHandler handler)
        {
            ConfigHandlers.Add(new HandlerConfiguration(topic, handler, false));
        }

        /// <summary>
        ///     This adds a handler for the contract topic. It will subscribe following topic:
        ///     {installationTopic}/{serviceProviderIdentifier}/{service}/{contract}/#
        /// </summary>
        /// <param name="service">The service part of the topic</param>
        /// <param name="contract">The contract part of the topic</param>
        /// <param name="handler">The handler to process the contract specific topics</param>

        // {installationTopic}/{serviceProviderIdentifier}/{service}/{contract}/{contract-specific-path}
        public void WithContractHandler(string service, string contract, ServiceProviderMessageHandler handler)
        {
            var topicPartToMatch = $"{service}/{contract}";
            ConfigHandlers.Add(new HandlerConfiguration(topicPartToMatch, handler, true));
        }

        /// <summary>
        ///     This allows to set a health check evaluator function that can be used to monitor the health status of the service
        ///     provider. The function should return a HealthStatus indicating the current health state.
        /// </summary>
        /// <param name="healthCheckEvaluator">The function to evaluate the health status.</param>
        public void WithHealthCheckEvaluator(Func<HealthStatus> healthCheckEvaluator)
        {
            HealthCheckStatusProviderFunc = healthCheckEvaluator;
        }
    }

    /// <summary>
    ///     Final builder stage: register message handlers and optionally override the default system-control handlers.
    ///     <see cref="WithRestartCallback" /> and <see cref="WithLogLevelChangeCallback" /> are optional — when omitted, the
    ///     SDK's default handlers are used.
    /// </summary>
    public class ServiceProviderClientBuilder
    {
        private readonly ServiceProviderClientConfiguration _config;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ServiceProviderClientBuilder" /> class.
        /// </summary>
        /// <param name="config">The configuration being built.</param>
        public ServiceProviderClientBuilder(ServiceProviderClientConfiguration config)
        {
            _config = config;
        }

        /// <summary>
        ///     Configures the message handlers for the service provider.
        /// </summary>
        /// <remarks>
        ///     The callback runs on every successful (re)connection — the SDK re-invokes it (rebuilding handlers and
        ///     re-subscribing) whenever the operational connection is (re)established. Anything constructed inside it is rebuilt
        ///     each time, so construct shared/stateful objects once outside the callback and only wire them up here.
        /// </remarks>
        /// <param name="handlerSetupCallback">The callback action to configure handlers.</param>
        /// <returns>This builder, for chaining.</returns>
        public ServiceProviderClientBuilder WithHandlers(Action<IHandlerBuilder> handlerSetupCallback)
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
            return this;
        }

        /// <summary>
        ///     Overrides the default <c>restart</c> handler. Optional — when not called, the SDK's default restart handler is used.
        /// </summary>
        /// <param name="onRestartCallback">The handler invoked on a restart command.</param>
        /// <returns>This builder, for chaining.</returns>
        public ServiceProviderClientBuilder WithRestartCallback(ServiceProviderMessageHandler onRestartCallback)
        {
            _config.OnRestartCallback = onRestartCallback;
            return this;
        }

        /// <summary>
        ///     Overrides the default <c>logLevel/set</c> handler. Optional — when not called, the SDK's default handler is used,
        ///     which sets <see cref="SystemControl.LogLevelManager.CurrentLevel" />.
        /// </summary>
        /// <param name="onLogLevelChangedCallback">The handler invoked on a log-level-set command.</param>
        /// <param name="logLevelProviderCallback">
        ///     The callback providing the current log level for state publishing. Defaults to
        ///     <see cref="SystemControl.LogLevelManager.CurrentLevel" /> when not supplied.
        /// </param>
        /// <returns>This builder, for chaining.</returns>
        public ServiceProviderClientBuilder WithLogLevelChangeCallback(ServiceProviderMessageHandler onLogLevelChangedCallback, Func<LogLevel>? logLevelProviderCallback = null)
        {
            _config.OnLogLevelChangeCallback = onLogLevelChangedCallback;
            _config.CurrentLogLevelProviderCallback = logLevelProviderCallback;
            return this;
        }

        /// <summary>
        ///     Registers an async callback invoked once the service provider is fully operational — after it has registered,
        ///     connected, sent its declaration, subscribed its handlers, and published its initial SDK state. Optional.
        /// </summary>
        /// <remarks>
        ///     Invoked on every successful (re)connection: the SDK re-runs the full startup flow whenever the operational
        ///     connection is (re)established, so this fires again after each reconnect. Use it to (re)publish service state the
        ///     broker may have lost or that changed while offline.
        /// </remarks>
        /// <param name="onOperationalReady">The callback, receiving the publish surface and the application-stopping token.</param>
        /// <returns>This builder, for chaining.</returns>
        public ServiceProviderClientBuilder WithOnOperationalReady(Func<IServiceProviderPublish, CancellationToken, Task> onOperationalReady)
        {
            _config.OnOperationalReadyCallback = onOperationalReady;
            return this;
        }

        /// <summary>
        ///     Builds and returns the completed service provider client configuration.
        /// </summary>
        /// <returns>The configured <see cref="ServiceProviderClientConfiguration" />.</returns>
        public ServiceProviderClientConfiguration Build()
        {
            return _config;
        }
    }
}