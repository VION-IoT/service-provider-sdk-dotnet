using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using Vion.Contracts.Events.MeshToCloud;
using Vion.Contracts.Events.MeshToServiceProvider;
using Vion.Contracts.Events.ServiceProviderToMesh;
using Vion.Contracts.FlatBuffers.System.Health;
using Vion.Contracts.Mqtt;
using Vion.ServiceProvider.Sdk.Infrastructure;
using Vion.ServiceProvider.Sdk.JsonSerializationContexts;
using Vion.ServiceProvider.Sdk.RegistrationFlow.Extensions;
using Vion.ServiceProvider.Sdk.Setup;
using Vion.ServiceProvider.Sdk.SystemControl;
using Vion.Telemetry.Instrumentation;
using static Vion.Contracts.Mqtt.MqttUserProperties;
using ConnectionStatus = Vion.Contracts.Events.MeshToCloud.ConnectionStatus;
using HealthStatus = Vion.Contracts.Events.MeshToCloud.HealthStatus;
using MqttUserProperty = MQTTnet.Packets.MqttUserProperty;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    /// <summary>
    ///     Implementation of the service provider client that handles MQTT communication, registration, and message publishing.
    /// </summary>
    public partial class ServiceProviderClient : IServiceProviderClient, IServiceProviderClientHandler, IAsyncDisposable
    {
        private readonly ServiceProviderClientConfiguration _configuration;

        private readonly IMessageDispatcher _dispatcher;

        private readonly ILogger _logger;

        private readonly MqttClientFactory _mqttClientFactory;

        private readonly IMqttClient _operationalClient;

        private readonly SemaphoreSlim _startSemaphore = new(1, 1);

        private CancellationToken? _appStoppingToken;

        private MqttConnectionData? _connectionData;

        private MqttClientSubscribeOptions? _currentClientSubscriptionOptions;

        private ConcurrentBag<HandlerConfiguration> _handlers = new(); // Remove readonly

        private volatile Func<HealthStatus>? _healthStateProviderFunc;

        private volatile OperationalData? _operationalData;

        private CancellationTokenSource? _registrationCts;

        private string? _secret;

        private CancellationTokenSource? _setupSchemaCts;

        private int _shutdownCleanupCompleted;

        private volatile string? _topicComponentHealthState;

        /// <summary>
        ///     Initializes a new instance of the <see cref="ServiceProviderClient" /> class.
        /// </summary>
        /// <param name="configuration">The service provider client configuration.</param>
        /// <param name="mqttClientFactory">The factory for creating MQTT clients.</param>
        /// <param name="logger">The logger instance.</param>
        public ServiceProviderClient(ServiceProviderClientConfiguration configuration, MqttClientFactory mqttClientFactory, ILogger logger) : this(configuration,
            mqttClientFactory,
            logger,
            new MessageDispatcher(logger))
        {
        }

        private ServiceProviderClient(ServiceProviderClientConfiguration configuration, MqttClientFactory mqttClientFactory, ILogger logger, IMessageDispatcher dispatcher)
        {
            _mqttClientFactory = mqttClientFactory;
            _logger = logger;
            _configuration = configuration;
            _dispatcher = dispatcher;
            _operationalClient = _mqttClientFactory.CreateMqttClient();
            _operationalClient.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
            _operationalClient.DisconnectedAsync += OnDisconnectedAsync;
            _operationalClient.ConnectedAsync += OnConnectedAsync;
            _operationalClient.ConnectingAsync += OnConnectingAsync;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            // Only do app shutdown cleanup if app is actually stopping, and we haven't done it yet
            if (_appStoppingToken is { IsCancellationRequested: true } && Interlocked.CompareExchange(ref _shutdownCleanupCompleted, 1, 0) == 0)
            {
                try
                {
                    await OnAppStoppingAsync();
                }
                catch (Exception ex)
                {
                    LogDisposeCleanupFailed(ex);
                }
            }

            // Always clean up resources
            SafeCancelAndDispose(ref _setupSchemaCts, nameof(_setupSchemaCts));
            SafeCancelAndDispose(ref _registrationCts, nameof(_registrationCts));
            _operationalClient.Dispose();
            _startSemaphore.Dispose();
        }

        /// <inheritdoc />
        public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;

        /// <inheritdoc />
        public event Func<MqttClientConnectedEventArgs, Task>? ConnectedAsync;

        /// <inheritdoc />
        public event Func<MqttClientConnectingEventArgs, Task>? ConnectingAsync;

        /// <inheritdoc />
        public event Func<MqttClientDisconnectedEventArgs, Task>? DisconnectedAsync;

        /// <inheritdoc />
        public async Task StartAsync(CancellationToken? appStoppingToken)
        {
            await _startSemaphore.WaitAsync();
            try
            {
                _appStoppingToken = appStoppingToken;

                RegisterForAppShutdown(appStoppingToken);

                _connectionData = _configuration.ConnectionData;
                _secret = _configuration.Secret;
                var stoppingToken = _appStoppingToken!.Value;

                try
                {
                    // execute flow
                    _operationalData = await RegisterAsync(_connectionData, _secret, stoppingToken);
                    await ConnectOperationalClientAsync(stoppingToken);
                    var serviceProviderDeclarationPayload = await SendOptionalSetupSchemaAsync(stoppingToken);
                    await SendDeclarationAsync(_operationalData, serviceProviderDeclarationPayload, stoppingToken);

                    await SetupHandlersAsync(stoppingToken);

                    await PublishInitialStatesAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // Flow was cancelled due to disconnection (OnDisconnectedAsync called Cancel + StartAsync)
                    // The new StartAsync flow is already running, so we just exit this cancelled flow gracefully
                    LogStartupFlowCanceledByDisconnection();
                }

                // If stoppingToken.IsCancellationRequested == true, let the exception propagate (app shutdown)
            }
            finally
            {
                _startSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public Task<MqttClientPublishResult> PublishMessageAsync(string topic,
                                                                 Guid correlationId,
                                                                 CancellationToken cancellationToken,
                                                                 string? contentType = null,
                                                                 string? schema = null,
                                                                 ReadOnlyMemory<byte> payload = default,
                                                                 MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce,
                                                                 bool retain = false)
        {
            var msg = BuildMessage(topic,
                                   correlationId,
                                   contentType,
                                   schema,
                                   payload,
                                   qos,
                                   retain,
                                   null,
                                   null,
                                   null);
            LogPublishingMessage(correlationId, topic);
            return PublishRawAsync(_operationalClient, msg, cancellationToken);
        }

        /// <inheritdoc />
        public Task<MqttClientPublishResult> PublishResponseAsync(string topic,
                                                                  RequestStatus status,
                                                                  Guid correlationId,
                                                                  CancellationToken cancellationToken,
                                                                  string? contentType = null,
                                                                  string? schema = null,
                                                                  ReadOnlyMemory<byte> payload = default,
                                                                  string? errorCode = null,
                                                                  string? errorMessage = null,
                                                                  MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtLeastOnce)
        {
            var msg = BuildMessage(topic,
                                   correlationId,
                                   contentType,
                                   schema,
                                   payload,
                                   qos,
                                   false,
                                   status,
                                   errorCode,
                                   errorMessage);
            LogPublishingMessage(correlationId, topic);
            return PublishRawAsync(_operationalClient, msg, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<MqttClientPublishResult> PublishHealthStatusAsync(string topic,
                                                                            ConnectionStatus connectionStatus,
                                                                            HealthStatus healthStatus,
                                                                            DateTime since,
                                                                            IServiceProviderClientHandler client,
                                                                            Guid correlationId,
                                                                            bool retain,
                                                                            CancellationToken cancellationToken)
        {
            try
            {
                LogPublishingMessage(correlationId, topic);
                var flatBufferConnectionStatus = connectionStatus.ToFlatBufferConnectionStatus();
                var flatBufferHealthStatus = healthStatus.ToFlatBufferHealthStatus();
                var payload = FlatBufferPayloadFactory.CreateComponentHealthStatusPayload(client.ServiceProviderIdentifier!,
                                                                                          flatBufferConnectionStatus,
                                                                                          flatBufferHealthStatus,
                                                                                          since);
                var schema = nameof(ComponentHealthStatusPayload);
                var msg = new MqttApplicationMessageBuilder().WithTopic(topic)
                                                             .WithPayload(payload)
                                                             .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                                                             .WithContentType(MessageMimeTypes.FlatBuffer)
                                                             .WithCorrelationData(correlationId.ToByteArray())
                                                             .WithUserProperty(PublishedAt.Name, Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString(PublishedAt.Format)))
                                                             .WithUserProperty(Schema.Name, Encoding.UTF8.GetBytes(schema))
                                                             .WithRetainFlag(retain)
                                                             .Build();

                return await PublishRawAsync(_operationalClient, msg, cancellationToken);
            }
            catch (Exception ex)
            {
                LogHealthStatusPublishFailed(ex, correlationId, topic);
                return new MqttClientPublishResult(0, MqttClientPublishReasonCode.UnspecifiedError, ex.ToString(), []);
            }
        }

        /// <inheritdoc />
        public async Task PublishLogLevelStateAsync()
        {
            var correlationId = Guid.NewGuid();
            try
            {
                if (InstallationTopic == null || ServiceProviderIdentifier == null)
                {
                    return; // Not yet registered, skip publishing
                }

                var currentLevel = (_configuration.CurrentLogLevelProviderCallback ?? (() => LogLevelManager.CurrentLevel)).Invoke();
                var topic = $"{InstallationTopic}/{ServiceProviderIdentifier}{Topics.ServiceProviderLogLevelState}";
                var payload = JsonSerializer.SerializeToUtf8Bytes(new LogLevelStatePayload(currentLevel), ServiceProviderJsonContext.Default.LogLevelStatePayload);
                var result = await PublishMessageAsync(topic,
                                                       correlationId,
                                                       CancellationToken.None,
                                                       MessageMimeTypes.Json,
                                                       nameof(LogLevelStatePayload),
                                                       payload,
                                                       retain: true);

                if (!result.IsSuccess)
                {
                    LogLogLevelStatePublishUnsuccessful(result.ReasonCode, correlationId, topic);
                }
            }
            catch (Exception ex)
            {
                LogLogLevelStatePublishError(ex, correlationId);
            }
        }

        /// <inheritdoc />
        public string? InstallationTopic
        {
            get => _operationalData?.InstallationTopic;
        }

        /// <inheritdoc />
        public string? ServiceProviderIdentifier
        {
            get => _operationalData?.ConnectionData.ServiceProviderIdentifier;
        }

        private static MqttApplicationMessage BuildMessage(string topic,
                                                           Guid correlationId,
                                                           string? contentType,
                                                           string? schema,
                                                           ReadOnlyMemory<byte> payload,
                                                           MqttQualityOfServiceLevel qos,
                                                           bool retain,
                                                           RequestStatus? status,
                                                           string? errorCode,
                                                           string? errorMessage)
        {
            if (!payload.IsEmpty)
            {
                if (schema == null)
                {
                    throw new ArgumentException("A schema is required when a payload is provided.", nameof(schema));
                }

                if (contentType == null)
                {
                    throw new ArgumentException("A content type is required when a payload is provided.", nameof(contentType));
                }
            }

            var builder = new MqttApplicationMessageBuilder().WithTopic(topic)
                                                             .WithQualityOfServiceLevel(qos)
                                                             .WithCorrelationData(correlationId.ToByteArray())
                                                             .WithRetainFlag(retain)
                                                             .WithUserProperty(PublishedAt.Name, Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString(PublishedAt.Format)));

            if (!payload.IsEmpty)
            {
                builder.WithPayload(new ReadOnlySequence<byte>(payload));
            }

            if (contentType != null)
            {
                builder.WithContentType(contentType);
            }

            if (schema != null)
            {
                builder.WithUserProperty(Schema.Name, Encoding.UTF8.GetBytes(schema));
            }

            if (status.HasValue)
            {
                builder.WithUserProperty(Status.Name, Encoding.UTF8.GetBytes(status.Value.ToString().ToLowerInvariant()));
            }

            if (errorCode != null)
            {
                builder.WithUserProperty(ErrorCode.Name, Encoding.UTF8.GetBytes(errorCode));
            }

            if (errorMessage != null)
            {
                builder.WithUserProperty(ErrorMessage.Name, Encoding.UTF8.GetBytes(errorMessage));
            }

            return builder.Build();
        }

        private static async Task<MqttClientPublishResult> PublishRawAsync(IMqttClient client, MqttApplicationMessage msg, CancellationToken cancellationToken)
        {
            using var activity = MessageActivities.StartMessagePublishActivity(msg.Topic);
            if (Activity.Current?.Id is { } traceParent)
            {
                msg.UserProperties ??= [];
                msg.UserProperties.Add(new MqttUserProperty(TraceParent.Name, Encoding.UTF8.GetBytes(traceParent)));
            }

            try
            {
                var result = await client.PublishAsync(msg, cancellationToken);
                if (!result.IsSuccess)
                {
                    activity?.MarkFailed($"Publish failed: {result.ReasonCode} - {result.ReasonString}");
                }

                return result;
            }
            catch (Exception ex)
            {
                activity?.MarkFailed(ex);
                throw;
            }
        }

        private async Task PublishInitialStatesAsync(CancellationToken stoppingToken)
        {
            var topicComponentHealthState =
                ServiceProviderTopics.GetTopicComponentHealthState(_operationalData!.InstallationTopic, _operationalData!.ConnectionData.ServiceProviderIdentifier);
            await PublishHealthStatusAsync(topicComponentHealthState,
                                           ConnectionStatus.Online,
                                           HealthStatus.Unknown,
                                           DateTime.UtcNow,
                                           this,
                                           Guid.NewGuid(),
                                           true,
                                           stoppingToken);

            await PublishLogLevelStateAsync();
        }

        private void RegisterForAppShutdown(CancellationToken? appStoppingToken)
        {
            // Register for app shutdown - this is app-specific cleanup
            if (appStoppingToken != null)
            {
                appStoppingToken.Value.Register(() =>
                                                {
                                                    _ = Task.Run(async () =>
                                                                 {
                                                                     try
                                                                     {
                                                                         if (Interlocked.CompareExchange(ref _shutdownCleanupCompleted, 1, 0) == 0)
                                                                         {
                                                                             try
                                                                             {
                                                                                 await OnAppStoppingAsync();
                                                                             }
                                                                             catch (Exception ex)
                                                                             {
                                                                                 LogAppStoppingCleanupError(ex);
                                                                             }
                                                                         }
                                                                     }
                                                                     catch (Exception e)
                                                                     {
                                                                         LogShutdownCleanupError(e);
                                                                     }
                                                                 });
                                                });
            }
        }

        private Task SetupHandlersAsync(CancellationToken stoppingToken)
        {
            ConcurrentBag<HandlerConfiguration> newHandlers;
            if (_configuration.HandlerSetupCallback == null)
            {
                LogHandlerSetupCallbackNotConfigured();
                _healthStateProviderFunc = () => HealthStatus.Unknown; // todo maybe change to unhealthy?
                newHandlers = new ConcurrentBag<HandlerConfiguration>();
            }
            else
            {
                var handlerBuilder = _configuration.HandlerSetupCallback!.Invoke(_operationalData!.InstallationTopic,
                                                                                 _operationalData.ConnectionData.ServiceProviderIdentifier,
                                                                                 _configuration.DeclarationPayload!);
                newHandlers = new ConcurrentBag<HandlerConfiguration>(handlerBuilder.ConfigHandlers);

                _healthStateProviderFunc = handlerBuilder.HealthCheckStatusProviderFunc;
            }

            RegisterAdditionalHandlers(newHandlers);

            Interlocked.Exchange(ref _handlers, newHandlers); // Atomic swap of complete bag

            return InternalUpdateSubscriptionAsync(_handlers, stoppingToken);
        }

        private void RegisterAdditionalHandlers(ConcurrentBag<HandlerConfiguration> newHandlers)
        {
            var topicGetComponentHealth =
                ServiceProviderTopics.GetTopicGetComponentHealth(_operationalData!.InstallationTopic, _operationalData.ConnectionData.ServiceProviderIdentifier);
            RegisterHandler(topicGetComponentHealth,
                            async (_, message, correlationId, cancellationToken) =>
                            {
                                var healthStatus = _healthStateProviderFunc?.Invoke() ?? HealthStatus.Healthy;
                                var responseTopic = message.ResponseTopic;

                                if (!string.IsNullOrEmpty(responseTopic))
                                {
                                    await PublishHealthStatusAsync(responseTopic,
                                                                   ConnectionStatus.Online,
                                                                   healthStatus,
                                                                   DateTime.UtcNow,
                                                                   this,
                                                                   correlationId,
                                                                   false,
                                                                   cancellationToken);
                                }
                                else
                                {
                                    LogHealthGetWithoutResponseTopic(correlationId, topicGetComponentHealth);
                                }
                            },
                            newHandlers);

            var restartTopic = ServiceProviderTopics.GetRestartTopic(_operationalData!.InstallationTopic, _operationalData.ConnectionData.ServiceProviderIdentifier);
            RegisterHandler(restartTopic, _configuration.OnRestartCallback ?? DefaultRestartAsync, newHandlers);

            var logLevelTopic = ServiceProviderTopics.LogLevelSetTopic(_operationalData!.InstallationTopic, _operationalData.ConnectionData.ServiceProviderIdentifier);
            RegisterHandler(logLevelTopic, _configuration.OnLogLevelChangeCallback ?? DefaultSetLogLevelAsync, newHandlers);
        }

        private Task DefaultSetLogLevelAsync(IServiceProviderPublish publisher, MqttApplicationMessage message, Guid correlationId, CancellationToken cancellationToken)
        {
            LogLogLevelHandlerNotConfigured(correlationId);
            return Task.CompletedTask;
        }

        private Task DefaultRestartAsync(IServiceProviderPublish publisher, MqttApplicationMessage message, Guid correlationId, CancellationToken cancellationToken)
        {
            LogRestartHandlerNotConfigured(correlationId);
            return Task.CompletedTask;
        }

        private void RegisterHandler(string topic, ServiceProviderMessageHandler handler, ConcurrentBag<HandlerConfiguration> handlers)
        {
            handlers.Add(new HandlerConfiguration(topic, handler, false, topic));
        }

        private async Task InternalUpdateSubscriptionAsync(ConcurrentBag<HandlerConfiguration> handlers, CancellationToken cancellationToken)
        {
            var topics = handlers.Select(h => h.TopicFilter).ToHashSet();
            _currentClientSubscriptionOptions = new MqttClientSubscribeOptions { TopicFilters = topics.Select(t => new MqttTopicFilterBuilder().WithTopic(t).Build()).ToList() };
            var subscribeResult = await _operationalClient.SubscribeAsync(_currentClientSubscriptionOptions, cancellationToken);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                LogOperationalClientSubscribed(subscribeResult.ReasonString, string.Join(",\n    ", subscribeResult.Items.Select(i => $"{i.TopicFilter.Topic}: {i.ResultCode}")));
            }
        }

        private async Task OnAppStoppingAsync()
        {
            if (_topicComponentHealthState != null)
            {
                await PublishHealthStatusAsync(_topicComponentHealthState,
                                               ConnectionStatus.Offline,
                                               HealthStatus.Unknown,
                                               DateTime.UtcNow,
                                               this,
                                               Guid.NewGuid(),
                                               true,
                                               CancellationToken.None);
            }
            else
            {
                LogCannotPublishOfflineHealth();
            }

            while (!await _operationalClient.TryDisconnectAsync(MqttClientDisconnectOptionsReason.NormalDisconnection, "app shutdown"))
            {
                LogWaitingForOperationalClientDisconnect();
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        private async Task<ServiceProviderDeclarationPayload> SendOptionalSetupSchemaAsync(CancellationToken cancellationToken)
        {
            if (_configuration.SetupSchemaPayload != null)
            {
                _configuration.SetupSelectionPayload = await SendSetupSchemaAsync(cancellationToken);
                _configuration.DeclarationPayload = _configuration.DeclarationCallbackWithSetup!.Invoke(_configuration.SetupSelectionPayload, _configuration.SetupSchemaPayload!);
            }
            else
            {
                _configuration.DeclarationPayload = _configuration.DeclarationCallback!.Invoke();
            }

            return _configuration.DeclarationPayload;
        }

        private async Task ConnectOperationalClientAsync(CancellationToken cancellationToken)
        {
            var serviceProviderIdentifier = _operationalData!.ConnectionData.ServiceProviderIdentifier;

            // host
            var optionsBuilder = new MqttClientOptionsBuilder().WithClientId(_operationalData!.ClientId)
                                                               .WithProtocolVersion(MqttProtocolVersion.V500)
                                                               .WithTcpServer(_operationalData!.ConnectionData.Host, _operationalData!.ConnectionData.Port)
                                                               .WithCredentials(_operationalData!.Username, _operationalData!.Password);

            // last will
            _topicComponentHealthState = ServiceProviderTopics.GetTopicComponentHealthState(_operationalData!.InstallationTopic, serviceProviderIdentifier);
            optionsBuilder.WithWillTopic(_topicComponentHealthState)
                          .WithWillCorrelationData(Guid.NewGuid().ToByteArray())
                          .WithWillContentType(MessageMimeTypes.FlatBuffer)
                          .WithWillPayload(FlatBufferPayloadFactory.CreateComponentHealthStatusPayload(_operationalData.ConnectionData.ServiceProviderIdentifier,
                                                                                                       Contracts.FlatBuffers.System.Health.ConnectionStatus.Offline,
                                                                                                       Contracts.FlatBuffers.System.Health.HealthStatus.Unknown,
                                                                                                       null))
                          .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                          .WithWillRetain()
                          .WithWillUserProperty(Schema.Name, Encoding.UTF8.GetBytes(nameof(ComponentHealthStatusPayload)));

            var options = optionsBuilder.Build();

            LogConnectingOperationalClient(_operationalData.ConnectionData.Host, _operationalData.ConnectionData.Port);
            var result = await _operationalClient.ConnectAsync(options, cancellationToken);

            if (result.ResultCode == MqttClientConnectResultCode.Success)
            {
                LogConnectedOperationalClient();
                await PublishHealthStatusAsync(_topicComponentHealthState,
                                               ConnectionStatus.Online,
                                               HealthStatus.Unknown,
                                               DateTime.UtcNow,
                                               this,
                                               Guid.NewGuid(),
                                               true,
                                               cancellationToken);
            }
            else
            {
                LogOperationalClientConnectFailed(result.ResultCode);
            }
        }

        [LoggerMessage(Level = LogLevel.Debug, Message = "Received message (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogReceivedMessage(Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Error, Message = "Message will not be processed — reason: {Reason} (Topic={Topic})")]
        private partial void LogInvalidCorrelationId(string reason, string topic);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Message handling was canceled due to client shutdown (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogMessageHandlingCanceledByShutdown(Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Message handling was canceled (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogMessageHandlingCanceled(Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Error, Message = "An exception occurred while processing the message (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogMessageProcessingError(Exception exception, Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Publishing message (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogPublishingMessage(Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Warning,
                       Message =
                           "Received logLevel/set command but no handler is configured — override WithLogLevelChangeCallback or use AddVionServiceProvider (CorrelationId={CorrelationId})")]
        private partial void LogLogLevelHandlerNotConfigured(Guid correlationId);

        [LoggerMessage(Level = LogLevel.Warning,
                       Message =
                           "Received restart command but no handler is configured — override WithRestartCallback or use AddVionServiceProvider (CorrelationId={CorrelationId})")]
        private partial void LogRestartHandlerNotConfigured(Guid correlationId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Error during app-shutdown cleanup")]
        private partial void LogDisposeCleanupFailed(Exception exception);

        [LoggerMessage(Level = LogLevel.Information, Message = "Startup flow canceled due to disconnection — a new flow is already initiated")]
        private partial void LogStartupFlowCanceledByDisconnection();

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to publish health status (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogHealthStatusPublishFailed(Exception exception, Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to publish log-level state (ReasonCode={ReasonCode}, CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogLogLevelStatePublishUnsuccessful(MqttClientPublishReasonCode reasonCode, Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to publish log-level state change (CorrelationId={CorrelationId})")]
        private partial void LogLogLevelStatePublishError(Exception exception, Guid correlationId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Error during app-shutdown cleanup in OnAppStopping")]
        private partial void LogAppStoppingCleanupError(Exception exception);

        [LoggerMessage(Level = LogLevel.Error, Message = "Error during app-shutdown cleanup")]
        private partial void LogShutdownCleanupError(Exception exception);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Handler setup callback is not configured")]
        private partial void LogHandlerSetupCallbackNotConfigured();

        [LoggerMessage(Level = LogLevel.Warning,
                       Message = "Received component-health get without a response topic — cannot send health status response (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogHealthGetWithoutResponseTopic(Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Information, Message = "Subscribed operational client (Reason={Reason}, Items={Items})")]
        private partial void LogOperationalClientSubscribed(string? reason, string items);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Cannot publish offline health — not yet connected operationally")]
        private partial void LogCannotPublishOfflineHealth();

        [LoggerMessage(Level = LogLevel.Information, Message = "Waiting for operational client to disconnect")]
        private partial void LogWaitingForOperationalClientDisconnect();

        [LoggerMessage(Level = LogLevel.Information, Message = "Connecting to operational MQTT broker (Host={Host}, Port={Port})")]
        private partial void LogConnectingOperationalClient(string host, int port);

        [LoggerMessage(Level = LogLevel.Information, Message = "Connected to operational MQTT broker")]
        private partial void LogConnectedOperationalClient();

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to connect to operational MQTT broker (ResultCode={ResultCode})")]
        private partial void LogOperationalClientConnectFailed(MqttClientConnectResultCode resultCode);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Received setup selection with mismatched correlation data (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogSetupSelectionCorrelationMismatch(Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Received null setup selection payload (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogNullSetupSelectionPayload(Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Error, Message = "Setup selection validation callback is not configured (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogSetupSelectionValidationCallbackNotConfigured(Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Setup selection validation failed (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogSetupSelectionValidationFailed(Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to deserialize setup selection payload (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogSetupSelectionDeserializationError(Exception exception, Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Service-provider startup is blocked until setup selection is received (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogWaitingForSetupSelection(Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Information,
                       Message =
                           "Published retained setup schema — waiting for selection response (PublishCount={PublishCount}, ReasonCode={ReasonCode}, CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogSetupSchemaPublished(int publishCount, MqttClientPublishReasonCode reasonCode, Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Warning,
                       Message =
                           "Failed to publish setup schema — will retry (PublishCount={PublishCount}, ReasonCode={ReasonCode}, ReasonString={ReasonString}, CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogSetupSchemaPublishUnsuccessful(int publishCount, MqttClientPublishReasonCode reasonCode, string? reasonString, Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Setup selection canceled — loop aborted (PublishCount={PublishCount}, CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogSetupSelectionCanceled(int publishCount, Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to publish setup schema — will retry shortly (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogSetupSchemaPublishRetrying(Exception exception, Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Warning,
                       Message = "Setup selection canceled during error retry (PublishCount={PublishCount}, CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogSetupSelectionCanceledDuringRetry(int publishCount, Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Information, Message = "Setup selection received (PublishCount={PublishCount}, CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogSetupSelectionReceived(int publishCount, Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to unsubscribe from setup selection topic (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogSetupSelectionUnsubscribeFailed(Exception exception, Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Debug,
                       Message = "Received registration message (Topic={Topic}, ContentType={ContentType}, Schema={Schema}, CorrelationId={CorrelationId})")]
        private partial void LogReceivedRegistrationMessage(string topic, string? contentType, string? schema, Guid correlationId);

        [LoggerMessage(Level = LogLevel.Information,
                       Message =
                           "Registration accepted (InstallationTopic={InstallationTopic}, ClientId={ClientId}, Host={Host}, Username={Username}, CorrelationId={CorrelationId})")]
        private partial void LogRegistrationAccepted(string installationTopic, string clientId, string host, string username, Guid correlationId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to deserialize registration accepted payload (CorrelationId={CorrelationId})")]
        private partial void LogRegistrationAcceptedDeserializationError(Exception exception, Guid correlationId);

        [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected registration message (Topic={Topic}, Reason={Reason}, CorrelationId={CorrelationId})")]
        private partial void LogUnexpectedRegistrationMessage(string topic, string? reason, Guid correlationId);

        [LoggerMessage(Level = LogLevel.Warning,
                       Message =
                           "Registration client disconnected — will republish retained message on reconnect (ReasonString={ReasonString}, Reason={Reason}, CorrelationId={CorrelationId})")]
        private partial void LogRegistrationClientDisconnected(string? reasonString, MqttClientDisconnectReason reason, Guid correlationId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Service-provider startup is blocked until registration is accepted (CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogWaitingForRegistration(Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Information,
                       Message =
                           "Published retained registration request — waiting for response (PublishCount={PublishCount}, ReasonCode={ReasonCode}, CorrelationId={CorrelationId})")]
        private partial void LogRegistrationRequestPublished(int publishCount, MqttClientPublishReasonCode reasonCode, Guid correlationId);

        [LoggerMessage(Level = LogLevel.Warning,
                       Message =
                           "Failed to publish registration request — will retry (PublishCount={PublishCount}, ReasonCode={ReasonCode}, ReasonString={ReasonString}, CorrelationId={CorrelationId})")]
        private partial void LogRegistrationRequestPublishUnsuccessful(int publishCount, MqttClientPublishReasonCode reasonCode, string? reasonString, Guid correlationId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Registration canceled — loop aborted (PublishCount={PublishCount}, CorrelationId={CorrelationId})")]
        private partial void LogRegistrationCanceled(int publishCount, Guid correlationId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to publish or connect for registration — will retry shortly (CorrelationId={CorrelationId})")]
        private partial void LogRegistrationPublishRetrying(Exception exception, Guid correlationId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Registration canceled during error retry (PublishCount={PublishCount}, CorrelationId={CorrelationId})")]
        private partial void LogRegistrationCanceledDuringRetry(int publishCount, Guid correlationId);

        [LoggerMessage(Level = LogLevel.Information,
                       Message = "Registration accepted — received operational credentials (PublishCount={PublishCount}, CorrelationId={CorrelationId})")]
        private partial void LogRegistrationCredentialsReceived(int publishCount, Guid correlationId);

        [LoggerMessage(Level = LogLevel.Information,
                       Message = "Received operational credentials " +
                                 "(InstallationTopic={InstallationTopic}, ClientId={ClientId}, Username={Username}, PasswordLength={PasswordLength}, " +
                                 "ServiceProviderIdentifier={ServiceProviderIdentifier}, Host={Host}, Port={Port}, CorrelationId={CorrelationId})")]
        private partial void LogOperationalCredentials(string installationTopic,
                                                       string clientId,
                                                       string username,
                                                       int passwordLength,
                                                       string serviceProviderIdentifier,
                                                       string host,
                                                       int port,
                                                       Guid correlationId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Connecting registration client to MQTT broker (Host={Host}, Port={Port})")]
        private partial void LogConnectingRegistrationClient(string host, int port);

        [LoggerMessage(Level = LogLevel.Information, Message = "Connected registration client (Reason={Reason}, ResultCode={ResultCode})")]
        private partial void LogConnectedRegistrationClient(string? reason, MqttClientConnectResultCode resultCode);

        [LoggerMessage(Level = LogLevel.Information, Message = "Subscribed registration client (Reason={Reason}, Items={Items})")]
        private partial void LogRegistrationClientSubscribed(string? reason, string items);

        [LoggerMessage(Level = LogLevel.Information, Message = "Published service-provider declaration (ReasonCode={ReasonCode}, CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogDeclarationPublished(MqttClientPublishReasonCode reasonCode, Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Warning,
                       Message =
                           "Failed to publish service-provider declaration (ReasonCode={ReasonCode}, ReasonString={ReasonString}, CorrelationId={CorrelationId}, Topic={Topic})")]
        private partial void LogDeclarationPublishUnsuccessful(MqttClientPublishReasonCode reasonCode, string? reasonString, Guid correlationId, string topic);

        [LoggerMessage(Level = LogLevel.Debug, Message = "CancellationTokenSource already disposed (Name={Name})")]
        private partial void LogCancellationTokenSourceAlreadyDisposed(Exception exception, string name);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to cancel CancellationTokenSource (Name={Name})")]
        private partial void LogCancellationTokenSourceCancelFailed(Exception exception, string name);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to dispose CancellationTokenSource (Name={Name})")]
        private partial void LogCancellationTokenSourceDisposeFailed(Exception exception, string name);

        #region callbacks

        private async Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            var topic = arg.ApplicationMessage.Topic;
            using var activity = MessageActivities.StartMessageConsumeActivity(topic, arg.ApplicationMessage.GetTraceParent());
            Guid correlationId;
            try
            {
                correlationId = arg.ApplicationMessage.GetCorrelationId();
            }
            catch (Exception exception)
            {
                // A message without a parseable correlation ID violates the wire contract — log and drop, don't dispatch.
                LogInvalidCorrelationId(exception.Message, topic);
                activity?.MarkFailed(exception.Message);
                return;
            }

            try
            {
                LogReceivedMessage(correlationId, topic);
                var fallback = ApplicationMessageReceivedAsync;
                await _dispatcher.DispatchAsync(arg.ApplicationMessage,
                                                this,
                                                _handlers,
                                                correlationId,
                                                fallback is null ? null : () => fallback.Invoke(arg),
                                                _appStoppingToken ?? CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                if (_appStoppingToken is { IsCancellationRequested: true })
                {
                    LogMessageHandlingCanceledByShutdown(correlationId, topic);
                }
                else
                {
                    LogMessageHandlingCanceled(correlationId, topic);
                }
            }
            catch (Exception exception)
            {
                LogMessageProcessingError(exception, correlationId, topic);
                activity?.MarkFailed(exception);
            }
        }

        private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            DisconnectedAsync?.Invoke(arg);

            // Only CANCEL, don't dispose (still in use by running loops)
            SafeCancel(_registrationCts, nameof(_registrationCts));
            SafeCancel(_setupSchemaCts, nameof(_setupSchemaCts));

            if (_appStoppingToken is { IsCancellationRequested: true })
            {
                // app shutdown in progress
                return Task.CompletedTask;
            }

            return StartAsync(_appStoppingToken!.Value);
        }

        private Task OnConnectingAsync(MqttClientConnectingEventArgs arg)
        {
            return ConnectingAsync?.Invoke(arg) ?? Task.CompletedTask;
        }

        private Task OnConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            return ConnectedAsync?.Invoke(arg) ?? Task.CompletedTask;
        }

        #endregion callbacks

        #region flow

        private async Task<ServiceProviderSetupSelectionPayload> SendSetupSchemaAsync(CancellationToken cancellationToken)
        {
            var serviceProviderIdentifier = _operationalData!.ConnectionData.ServiceProviderIdentifier;
            var installationTopic = _operationalData.InstallationTopic;

            // Build topics for setup schema request/response
            var setupSchemaTopic = ServiceProviderTopics.GetSetupSchemaTopic(installationTopic, serviceProviderIdentifier);
            var setupSelectionTopic = ServiceProviderTopics.GetSelectionTopic(installationTopic, serviceProviderIdentifier);
            var tcs = new TaskCompletionSource<ServiceProviderSetupSelectionPayload>();
            var correlationId = Guid.NewGuid();

            // Create cancellation source that cancels on disconnection OR app stopping
            SafeCancelAndDispose(ref _setupSchemaCts, nameof(_setupSchemaCts));
            _setupSchemaCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var loopCancellationToken = _setupSchemaCts.Token;

            // Subscribe to selection response
            var subscribeOptions = new MqttClientSubscribeOptions
                                   {
                                       TopicFilters =
                                       [
                                           new MqttTopicFilterBuilder().WithTopic(setupSelectionTopic)
                                                                       .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                                                                       .Build(),
                                       ],
                                   };

            // Handler for incoming selection response
            Task HandleSetupSelectionAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
            {
                if (eventArgs.ApplicationMessage.Topic != setupSelectionTopic)
                {
                    return Task.CompletedTask;
                }

                // Verify correlation data matches
                if (eventArgs.ApplicationMessage.CorrelationData != null && !eventArgs.ApplicationMessage.CorrelationData.SequenceEqual(correlationId.ToByteArray()))
                {
                    LogSetupSelectionCorrelationMismatch(correlationId, setupSelectionTopic);
                    return Task.CompletedTask;
                }

                try
                {
                    var selectionPayload = JsonSerializer.Deserialize(eventArgs.ApplicationMessage.Payload.ToArray(),
                                                                      ServiceProviderJsonContext.Default.ServiceProviderSetupSelectionPayload);

                    if (selectionPayload == null)
                    {
                        LogNullSetupSelectionPayload(correlationId, setupSelectionTopic);
                        return Task.CompletedTask;
                    }

                    // Validate selection
                    if (_configuration.SetupSelectionValidationCallback == null)
                    {
                        LogSetupSelectionValidationCallbackNotConfigured(correlationId, setupSelectionTopic);
                        return Task.CompletedTask;
                    }

                    if (!_configuration.SetupSelectionValidationCallback(selectionPayload, _configuration.SetupSchemaPayload!))
                    {
                        LogSetupSelectionValidationFailed(correlationId, setupSelectionTopic);
                        return Task.CompletedTask;
                    }

                    tcs.TrySetResult(selectionPayload);
                }
                catch (Exception ex)
                {
                    LogSetupSelectionDeserializationError(ex, correlationId, setupSelectionTopic);
                }

                return Task.CompletedTask;
            }

            _operationalClient.ApplicationMessageReceivedAsync += HandleSetupSelectionAsync;

            try
            {
                // Serialize setup schema payload once
                var payload = JsonSerializer.SerializeToUtf8Bytes(_configuration.SetupSchemaPayload, ServiceProviderJsonContext.Default.ServiceProviderSetupSchemaPayload);

                var needsPublish = true;
                var publishCount = 0;

                LogWaitingForSetupSelection(correlationId, setupSelectionTopic);

                // Initial subscription
                await _operationalClient.SubscribeAsync(subscribeOptions, loopCancellationToken);

                var msg = new MqttApplicationMessageBuilder().WithTopic(setupSchemaTopic)
                                                             .WithPayload(payload)
                                                             .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                                                             .WithContentType(MessageMimeTypes.Json)
                                                             .WithCorrelationData(correlationId.ToByteArray())
                                                             .WithResponseTopic(setupSelectionTopic)
                                                             .WithUserProperty(PublishedAt.Name, Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString(PublishedAt.Format)))
                                                             .WithUserProperty(Schema.Name, Encoding.UTF8.GetBytes(nameof(ServiceProviderSetupSchemaPayload)))
                                                             .WithRetainFlag()
                                                             .Build();

                while (!tcs.Task.IsCompleted)
                {
                    loopCancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (needsPublish)
                        {
                            publishCount++;
                            var publishResult = await PublishRawAsync(_operationalClient, msg, loopCancellationToken);

                            if (publishResult.IsSuccess)
                            {
                                needsPublish = false;
                                LogSetupSchemaPublished(publishCount, publishResult.ReasonCode, correlationId, setupSchemaTopic);
                            }
                            else
                            {
                                LogSetupSchemaPublishUnsuccessful(publishCount, publishResult.ReasonCode, publishResult.ReasonString, correlationId, setupSchemaTopic);
                            }
                        }

                        // Wait for either selection response or a short polling interval
                        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(1), loopCancellationToken));
                    }
                    catch (OperationCanceledException)
                    {
                        LogSetupSelectionCanceled(publishCount, correlationId, setupSelectionTopic);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        LogSetupSchemaPublishRetrying(ex, correlationId, setupSchemaTopic);
                        needsPublish = true; // Retry publish on next iteration

                        try
                        {
                            // Short delay before retry on error
                            await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5), loopCancellationToken));
                        }
                        catch (OperationCanceledException)
                        {
                            LogSetupSelectionCanceledDuringRetry(publishCount, correlationId, setupSelectionTopic);
                            throw;
                        }
                    }
                }

                LogSetupSelectionReceived(publishCount, correlationId, setupSelectionTopic);
                return await tcs.Task;
            }
            finally
            {
                _operationalClient.ApplicationMessageReceivedAsync -= HandleSetupSelectionAsync;
                try
                {
                    await _operationalClient.UnsubscribeAsync(setupSelectionTopic, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    LogSetupSelectionUnsubscribeFailed(ex, correlationId, setupSelectionTopic);
                }
            }
        }

        private async Task<OperationalData> RegisterAsync(MqttConnectionData connectionData, string secret, CancellationToken ct)
        {
            // Create cancellation source that cancels on disconnection OR app stopping
            SafeCancelAndDispose(ref _registrationCts, nameof(_registrationCts));
            _registrationCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var registrationToken = _registrationCts.Token;

            using var client = _mqttClientFactory.CreateMqttClient();
            try
            {
                var registrationAcceptedTopic = ServiceProviderTopics.GetRegistrationAcceptedTopic(secret);
                var registrationDeniedTopic = ServiceProviderTopics.GetRegistrationDeniedTopic(secret);

                var registrationOptions = new MqttClientOptionsBuilder().WithClientId(connectionData.ServiceProviderIdentifier)
                                                                        .WithProtocolVersion(MqttProtocolVersion.V500)
                                                                        .WithTcpServer(connectionData.Host, connectionData.Port)
                                                                        .Build();

                var mqttClientSubscribeOptions = new MqttClientSubscribeOptions
                                                 {
                                                     TopicFilters =
                                                     [
                                                         new MqttTopicFilterBuilder().WithTopic(registrationAcceptedTopic)
                                                                                     .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                                                                                     .Build(),
                                                         new MqttTopicFilterBuilder().WithTopic(registrationDeniedTopic)
                                                                                     .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                                                                                     .Build(),
                                                     ],
                                                 };

                var tcs = new TaskCompletionSource<ServiceProviderRegistrationAcceptedPayload>();
                var correlationId = Guid.NewGuid();

                client.ApplicationMessageReceivedAsync += eventArgs =>
                                                          {
                                                              using var activity = MessageActivities.StartMessageConsumeActivity(eventArgs.ApplicationMessage.Topic,
                                                                  eventArgs.ApplicationMessage.GetTraceParent());

                                                              if (_logger.IsEnabled(LogLevel.Debug))
                                                              {
                                                                  LogReceivedRegistrationMessage(eventArgs.ApplicationMessage.Topic,
                                                                                                 eventArgs.ApplicationMessage.ContentType,
                                                                                                 eventArgs.ApplicationMessage
                                                                                                          .UserProperties
                                                                                                          ?.FirstOrDefault(p => p.Name == Schema.Name)
                                                                                                          ?.ReadValueAsString(),
                                                                                                 correlationId);
                                                              }

                                                              // Registration phase handling
                                                              if (eventArgs.ApplicationMessage.Topic == registrationAcceptedTopic)
                                                              {
                                                                  try
                                                                  {
                                                                      var acceptedPayload = JsonSerializer.Deserialize(eventArgs.ApplicationMessage.Payload.ToArray(),
                                                                                                                       ServiceProviderJsonContext.Default
                                                                                                                           .ServiceProviderRegistrationAcceptedPayload);

                                                                      LogRegistrationAccepted(acceptedPayload.InstallationTopic,
                                                                                              acceptedPayload.ClientId,
                                                                                              acceptedPayload.Host,
                                                                                              acceptedPayload.Username,
                                                                                              correlationId);
                                                                      tcs.TrySetResult(acceptedPayload);
                                                                  }
                                                                  catch (Exception ex)
                                                                  {
                                                                      LogRegistrationAcceptedDeserializationError(ex, correlationId);
                                                                  }

                                                                  return Task.CompletedTask;
                                                              }

                                                              LogUnexpectedRegistrationMessage(eventArgs.ApplicationMessage.Topic, eventArgs.ResponseReasonString, correlationId);

                                                              return Task.CompletedTask;
                                                          };
                var needsPublish = true;
                var publishCount = 0;

                client.DisconnectedAsync += e =>
                                            {
                                                LogRegistrationClientDisconnected(e.ReasonString, e.Reason, correlationId);
                                                needsPublish = true; // Trigger republish on reconnect since broker might have lost retained message
                                                return Task.CompletedTask;
                                            };

                // publish registration
                var topic = $"{Topics.ServiceProviderRegistrationRequest}/{secret}";
                var payload = JsonSerializer.SerializeToUtf8Bytes(new ServiceProviderRegistrationRequestPayload(connectionData.ServiceProviderIdentifier),
                                                                  ServiceProviderJsonContext.Default.ServiceProviderRegistrationRequestPayload);

                LogWaitingForRegistration(correlationId, registrationAcceptedTopic);

                var msg = new MqttApplicationMessageBuilder().WithTopic(topic)
                                                             .WithPayload(payload)
                                                             .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                                                             .WithContentType(MessageMimeTypes.Json)
                                                             .WithCorrelationData(correlationId.ToByteArray())
                                                             .WithUserProperty(PublishedAt.Name, Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString(PublishedAt.Format)))
                                                             .WithUserProperty(Schema.Name, Encoding.UTF8.GetBytes(nameof(ServiceProviderRegistrationRequestPayload)))
                                                             .WithRetainFlag()
                                                             .Build();

                while (!tcs.Task.IsCompleted)
                {
                    registrationToken.ThrowIfCancellationRequested();

                    try
                    {
                        if (!client.IsConnected)
                        {
                            await ConnectRegistrationClientAsync(connectionData, registrationOptions, mqttClientSubscribeOptions, client, registrationToken);
                            needsPublish = true; // Republish after reconnection
                        }

                        if (needsPublish)
                        {
                            publishCount++;
                            var publishResult = await PublishRawAsync(client, msg, registrationToken);

                            if (publishResult.IsSuccess)
                            {
                                needsPublish = false;
                                LogRegistrationRequestPublished(publishCount, publishResult.ReasonCode, correlationId);
                            }
                            else
                            {
                                LogRegistrationRequestPublishUnsuccessful(publishCount, publishResult.ReasonCode, publishResult.ReasonString, correlationId);
                            }
                        }

                        // Wait for either registration response or a short polling interval
                        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(1), registrationToken));
                    }
                    catch (OperationCanceledException)
                    {
                        LogRegistrationCanceled(publishCount, correlationId);
                        throw;
                    }
                    catch (Exception e)
                    {
                        LogRegistrationPublishRetrying(e, correlationId);
                        needsPublish = true; // Retry publish on next iteration
                        try
                        {
                            // Short delay before retry on error
                            await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5), registrationToken));
                        }
                        catch (OperationCanceledException)
                        {
                            LogRegistrationCanceledDuringRetry(publishCount, correlationId);
                            throw;
                        }
                    }
                }

                var acceptedPayload = await tcs.Task;

                LogRegistrationCredentialsReceived(publishCount, correlationId);
                var operationalData = new OperationalData(new MqttConnectionData(connectionData.ServiceProviderIdentifier, acceptedPayload.Host, acceptedPayload.Port),
                                                          acceptedPayload.InstallationTopic,
                                                          acceptedPayload.ClientId,
                                                          acceptedPayload.Username,
                                                          acceptedPayload.Password);
                LogOperationalCredentials(operationalData.InstallationTopic,
                                          operationalData.ClientId,
                                          operationalData.Username,
                                          operationalData.Password.Length,
                                          operationalData.ConnectionData.ServiceProviderIdentifier,
                                          operationalData.ConnectionData.Host,
                                          operationalData.ConnectionData.Port,
                                          correlationId);
                return operationalData;
            }
            finally
            {
                if (client is { IsConnected: true })
                {
                    await client.DisconnectAsync(reasonString: "Registration done or canceled", cancellationToken: CancellationToken.None);
                }
            }
        }

        private async Task ConnectRegistrationClientAsync(MqttConnectionData connectionData,
                                                          MqttClientOptions registrationOptions,
                                                          MqttClientSubscribeOptions mqttClientSubscribeOptions,
                                                          IMqttClient client,
                                                          CancellationToken ct)
        {
            LogConnectingRegistrationClient(connectionData.Host, connectionData.Port);
            var result = await client.ConnectAsync(registrationOptions, ct);
            LogConnectedRegistrationClient(result.ReasonString, result.ResultCode);

            var subscribeResult = await client.SubscribeAsync(mqttClientSubscribeOptions, ct);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                LogRegistrationClientSubscribed(subscribeResult.ReasonString, string.Join(",\n    ", subscribeResult.Items.Select(i => $"{i.TopicFilter.Topic}: {i.ResultCode}")));
            }
        }

        private async Task SendDeclarationAsync(OperationalData operationalData, ServiceProviderDeclarationPayload declaration, CancellationToken cancellationToken)
        {
            var installationTopic = operationalData.InstallationTopic;
            var payload = JsonSerializer.SerializeToUtf8Bytes(declaration, ServiceProviderJsonContext.Default.ServiceProviderDeclarationPayload);
            var topic = ServiceProviderTopics.GetServiceProviderDeclarationTopic(installationTopic, operationalData.ConnectionData.ServiceProviderIdentifier);
            var correlationId = Guid.NewGuid();

            var msg = new MqttApplicationMessageBuilder().WithTopic(topic)
                                                         .WithPayload(payload)
                                                         .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                                                         .WithContentType(MessageMimeTypes.Json)
                                                         .WithCorrelationData(correlationId.ToByteArray())
                                                         .WithUserProperty(PublishedAt.Name, Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString(PublishedAt.Format)))
                                                         .WithUserProperty(Schema.Name, Encoding.UTF8.GetBytes(nameof(ServiceProviderDeclarationPayload)))
                                                         .WithRetainFlag()
                                                         .Build();

            var result = await PublishRawAsync(_operationalClient, msg, cancellationToken);
            if (result.IsSuccess)
            {
                LogDeclarationPublished(result.ReasonCode, correlationId, topic);
            }
            else
            {
                LogDeclarationPublishUnsuccessful(result.ReasonCode, result.ReasonString, correlationId, topic);
            }
        }

        // Safe cancellation helper
        private void SafeCancel(CancellationTokenSource? cts, string name)
        {
            try
            {
                cts?.Cancel();
            }
            catch (ObjectDisposedException e)
            {
                // Already disposed, ignore
                LogCancellationTokenSourceAlreadyDisposed(e, name);
            }
        }

        // Safe cancellation and disposal
        private void SafeCancelAndDispose(ref CancellationTokenSource? cts, string name)
        {
            var oldCts = Interlocked.Exchange(ref cts, null);
            if (oldCts != null)
            {
                try
                {
                    oldCts.Cancel();
                }
                catch (Exception e)
                {
                    LogCancellationTokenSourceCancelFailed(e, name);
                }

                try
                {
                    oldCts.Dispose();
                }
                catch (Exception e)
                {
                    LogCancellationTokenSourceDisposeFailed(e, name);
                }
            }
        }

        #endregion flow
    }
}