using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
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
using Shared.Contracts.Events.MeshToCloud;
using Shared.Contracts.Events.ServiceProviderToMesh;
using Shared.Contracts.FlatBuffers.System.Health;
using Shared.Contracts.Mqtt;
using Vion.ServiceProvider.Sdk.RegistrationFlow.Extensions;
using static Shared.Contracts.Mqtt.MqttUserProperties;
using ConnectionStatus = Shared.Contracts.Events.MeshToCloud.ConnectionStatus;
using HealthStatus = Shared.Contracts.Events.MeshToCloud.HealthStatus;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    public class ServiceProviderClient : IServiceProviderClient, IServiceProviderClientHandler, IAsyncDisposable
    {
        private readonly ServiceProviderClientConfiguration _configuration;

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

        public ServiceProviderClient(ServiceProviderClientConfiguration configuration, MqttClientFactory mqttClientFactory, ILogger logger)
        {
            _mqttClientFactory = mqttClientFactory;
            _logger = logger;
            _configuration = configuration;
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
                    _logger.LogWarning(ex, "Error during app shutdown cleanup");
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
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // Flow was cancelled due to disconnection (OnDisconnectedAsync called Cancel + StartAsync)
                    // The new StartAsync flow is already running, so we just exit this cancelled flow gracefully
                    _logger.LogInformation("Startup flow cancelled due to disconnection - new flow already initiated");
                }

                // If stoppingToken.IsCancellationRequested == true, let the exception propagate (app shutdown)
            }
            finally
            {
                _startSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public Task PublishAsync(string topic, string schema, string contentType, byte[] payload, CancellationToken cancellationToken)
        {
            // publish
            var msg = new MqttApplicationMessageBuilder().WithTopic(topic)
                                                         .WithPayload(payload)
                                                         .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                                                         .WithContentType(contentType)
                                                         .WithUserProperty(PublishedAt.Name, Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString(PublishedAt.Format)))
                                                         .WithUserProperty(Schema.Name, Encoding.UTF8.GetBytes(schema))
                                                         .WithRetainFlag()
                                                         .Build();

            return PublishAsync(msg, cancellationToken);
        }

        public async Task PublishAsync(MqttApplicationMessage msg, CancellationToken cancellationToken)
        {
            try
            {
                await _operationalClient.PublishAsync(msg, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed publishing {Topic}", msg.Topic);
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

        public async Task PublishHealthStatusAsync(string topic,
                                                   ConnectionStatus connectionStatus,
                                                   HealthStatus healthStatus,
                                                   DateTime since,
                                                   IServiceProviderClientHandler client,
                                                   byte[]? correlationData,
                                                   bool retain,
                                                   CancellationToken cancellationToken)
        {
            correlationData ??= Guid.NewGuid().ToByteArray();
            var flatBufferConnectionStatus = connectionStatus.ToFlatBufferConnectionStatus();
            var flatBufferHealthStatus = healthStatus.ToFlatBufferHealthStatus();
            var payload = FlatBufferPayloadFactory.CreateComponentHealthStatusPayload(client.ServiceProviderIdentifier!, flatBufferConnectionStatus, flatBufferHealthStatus, since);
            var schema = nameof(ComponentHealthStatusPayload);
            var msg = new MqttApplicationMessageBuilder().WithTopic(topic)
                                                         .WithPayload(payload)
                                                         .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                                                         .WithContentType(MessageMimeTypes.FlatBuffer)
                                                         .WithCorrelationData(correlationData)
                                                         .WithUserProperty(PublishedAt.Name, Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString(PublishedAt.Format)))
                                                         .WithUserProperty(Schema.Name, Encoding.UTF8.GetBytes(schema))
                                                         .WithRetainFlag(retain)
                                                         .Build();

            try
            {
                await client.PublishAsync(msg, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed publishing {Topic}", topic);
            }
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
                                                                                 _logger.LogError(ex, "Error during app shutdown cleanup in OnAppStoppingAsync");
                                                                             }
                                                                         }
                                                                     }
                                                                     catch (Exception e)
                                                                     {
                                                                         _logger.LogError(e, "Error during app shutdown cleanup");
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
                _logger.LogWarning("Handler setup callback is not configured");
                _healthStateProviderFunc = () => HealthStatus.Unknown; // todo maybe change to unhealthy?
                newHandlers = new ConcurrentBag<HandlerConfiguration>();
            }
            else
            {
                var handlerBuilder = _configuration.HandlerSetupCallback!.Invoke(_operationalData!.InstallationTopic, _operationalData.ConnectionData.ServiceProviderIdentifier);
                newHandlers = new ConcurrentBag<HandlerConfiguration>(handlerBuilder.ConfigHandlers);

                _healthStateProviderFunc = handlerBuilder.HealthCheckStatusProviderFunc;
            }

            var topicGetComponentHealth = $"{_operationalData!.InstallationTopic}/{_operationalData.ConnectionData.ServiceProviderIdentifier}{Topics.ComponentHealthGet}";
            RegisterHandler(topicGetComponentHealth,
                            async (client, eventArgs) =>
                            {
                                var responseTopic = eventArgs.ApplicationMessage.ResponseTopic;
                                var healthStatus = _healthStateProviderFunc?.Invoke() ?? HealthStatus.Healthy;
                                await PublishHealthStatusAsync(responseTopic,
                                                               ConnectionStatus.Online,
                                                               healthStatus,
                                                               DateTime.UtcNow,
                                                               client,
                                                               eventArgs.ApplicationMessage.CorrelationData,
                                                               false,
                                                               stoppingToken);
                                await PublishHealthStatusAsync(_topicComponentHealthState!,
                                                               ConnectionStatus.Online,
                                                               healthStatus,
                                                               DateTime.UtcNow,
                                                               client,
                                                               null,
                                                               true,
                                                               stoppingToken);
                            },
                            newHandlers);

            Interlocked.Exchange(ref _handlers, newHandlers); // Atomic swap of complete bag

            return InternalUpdateSubscriptionAsync(_handlers, stoppingToken);
        }

        private void RegisterHandler(string topic,
                                     Func<IServiceProviderClientHandler, MqttApplicationMessageReceivedEventArgs, Task> handler,
                                     ConcurrentBag<HandlerConfiguration> handlers)
        {
            if (topic.Contains("+") || topic.Contains("#"))
            {
                // use something like MQTTnet.Extensions.TopicTemplate.MqttTopicTemplate to match generic topics with wildcards, e.g. for contract handlers
                // and update the OnApplicationMessageReceivedAsync to match incoming message topics to the registered handlers using the topic template matching
                throw new ValidationException("Invalid topic for handler, no wildcards allowed");
            }

            // subscribe contract topic
            handlers.Add(new HandlerConfiguration(topic, handler, false, topic));
        }

        private async Task InternalUpdateSubscriptionAsync(ConcurrentBag<HandlerConfiguration> handlers, CancellationToken cancellationToken)
        {
            var topics = handlers.Select(h => h.TopicFilter).ToHashSet();
            _currentClientSubscriptionOptions = new MqttClientSubscribeOptions { TopicFilters = topics.Select(t => new MqttTopicFilterBuilder().WithTopic(t).Build()).ToList() };
            var x = await _operationalClient.SubscribeAsync(_currentClientSubscriptionOptions, cancellationToken);
            _logger.LogInformation("Subscribed for operational client:{Reason} ({Items})",
                                   x.ReasonString,
                                   string.Join(", ", x.Items.Select(i => $"{i.TopicFilter.Topic}: {i.ResultCode}")));
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
                                               Guid.NewGuid().ToByteArray(),
                                               true,
                                               CancellationToken.None);
            }
            else
            {
                _logger.LogWarning("Cannot publish offline health - not yet connected operationally");
            }

            while (!await _operationalClient.TryDisconnectAsync(MqttClientDisconnectOptionsReason.NormalDisconnection, "app shutdown"))
            {
                _logger.LogInformation("Waiting for operational client to disconnect...");
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        private async Task<ServiceProviderDeclarationPayload> SendOptionalSetupSchemaAsync(CancellationToken cancellationToken)
        {
            ServiceProviderDeclarationPayload? serviceProviderDeclarationPayload;
            if (_configuration.SetupSchemaPayload != null)
            {
                var setupSelection = await SendSetupSchemaAsync(cancellationToken);
                serviceProviderDeclarationPayload = _configuration.DeclarationCallbackWithSetup!.Invoke(setupSelection, _configuration.SetupSchemaPayload!);
            }
            else
            {
                serviceProviderDeclarationPayload = _configuration.DeclarationCallback!.Invoke();
            }

            return serviceProviderDeclarationPayload;
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
            _topicComponentHealthState = $"{_operationalData.InstallationTopic}/{serviceProviderIdentifier}{Topics.ComponentHealthState}";
            optionsBuilder.WithWillTopic(_topicComponentHealthState)
                          .WithWillCorrelationData(Guid.NewGuid().ToByteArray())
                          .WithWillContentType(MessageMimeTypes.FlatBuffer)
                          .WithWillPayload(FlatBufferPayloadFactory.CreateComponentHealthStatusPayload(_operationalData.ConnectionData.ServiceProviderIdentifier,
                                                                                                       Shared.Contracts.FlatBuffers.System.Health.ConnectionStatus.Offline,
                                                                                                       Shared.Contracts.FlatBuffers.System.Health.HealthStatus.Unknown,
                                                                                                       null))
                          .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                          .WithWillRetain()
                          .WithWillUserProperty(Schema.Name, Encoding.UTF8.GetBytes(nameof(ComponentHealthStatusPayload)));

            var options = optionsBuilder.Build();

            _logger.LogInformation("Connecting to operational MQTT broker at {Host}:{Port}...", _operationalData.ConnectionData.Host, _operationalData.ConnectionData.Port);
            var result = await _operationalClient.ConnectAsync(options, cancellationToken);

            if (result.ResultCode == MqttClientConnectResultCode.Success)
            {
                _logger.LogInformation("Connected to operational MQTT broker");
                await PublishHealthStatusAsync(_topicComponentHealthState,
                                               ConnectionStatus.Online,
                                               HealthStatus.Unknown,
                                               DateTime.UtcNow,
                                               this,
                                               Guid.NewGuid().ToByteArray(),
                                               true,
                                               cancellationToken);
            }
            else
            {
                _logger.LogWarning("Failed to connect to operational MQTT broker. Reason: {ReasonString} ({ResultCode})", result.ReasonString, result.ResultCode);
            }
        }

        #region callbacks

        private async Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Received message on topic {Topic}, content type {ContentType}, schema {Schema}",
                                 arg.ApplicationMessage.Topic,
                                 arg.ApplicationMessage.ContentType,
                                 arg.ApplicationMessage.UserProperties?.FirstOrDefault(p => p.Name == Schema.Name)?.ReadValueAsString());
            }

            // dispatch to user handlers
            var handlers = _handlers.Where(h => arg.ApplicationMessage.Topic.Contains(h.TopicPartToMatch)).ToList();

            if (handlers.Any())
            {
                foreach (var handler in handlers)
                {
                    await handler.Handler.Invoke(this, arg);
                }

                return;
            }

            await (ApplicationMessageReceivedAsync?.Invoke(arg) ?? Task.CompletedTask);
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

        private async Task<Payloads.ServiceProviderSetupSelectionPayload> SendSetupSchemaAsync(CancellationToken cancellationToken)
        {
            var serviceProviderIdentifier = _operationalData!.ConnectionData.ServiceProviderIdentifier;
            var installationTopic = _operationalData.InstallationTopic;

            // Build topics for setup schema request/response
            var setupSchemaTopic = $"{installationTopic}/{serviceProviderIdentifier}/serviceProvider/setup/schema";
            var setupSelectionTopic = $"{installationTopic}/{serviceProviderIdentifier}/serviceProvider/setup/selection";

            var tcs = new TaskCompletionSource<Payloads.ServiceProviderSetupSelectionPayload>();
            var correlationData = Guid.NewGuid().ToByteArray();

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
                if (eventArgs.ApplicationMessage.CorrelationData != null && !eventArgs.ApplicationMessage.CorrelationData.SequenceEqual(correlationData))
                {
                    _logger.LogWarning("Received setup selection with mismatched correlation data");
                    return Task.CompletedTask;
                }

                try
                {
                    var selectionPayload = JsonSerializer.Deserialize(eventArgs.ApplicationMessage.Payload.ToArray(),
                                                                      JsonSerializationContexts.ServiceProviderJsonContext.Default.ServiceProviderSetupSelectionPayload);

                    if (selectionPayload == null)
                    {
                        _logger.LogWarning("Received null setup selection payload");
                        return Task.CompletedTask;
                    }

                    // Validate selection
                    if (_configuration.SetupSelectionValidationCallback == null)
                    {
                        _logger.LogError("Setup selection validation callback is not configured");
                        return Task.CompletedTask;
                    }

                    if (!_configuration.SetupSelectionValidationCallback(selectionPayload, _configuration.SetupSchemaPayload))
                    {
                        _logger.LogWarning("Setup selection validation failed, see logs!");
                        return Task.CompletedTask;
                    }

                    tcs.TrySetResult(selectionPayload);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize setup selection payload");
                }

                return Task.CompletedTask;
            }

            _operationalClient.ApplicationMessageReceivedAsync += HandleSetupSelectionAsync;

            try
            {
                // Serialize setup schema payload once
                var payload = JsonSerializer.SerializeToUtf8Bytes(_configuration.SetupSchemaPayload,
                                                                  JsonSerializationContexts.ServiceProviderJsonContext.Default.ServiceProviderSetupSchemaPayload);

                var msg = new MqttApplicationMessageBuilder().WithTopic(setupSchemaTopic)
                                                             .WithPayload(payload)
                                                             .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                                                             .WithContentType(MessageMimeTypes.Json)
                                                             .WithCorrelationData(correlationData)
                                                             .WithResponseTopic(setupSelectionTopic)
                                                             .WithUserProperty(PublishedAt.Name, Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString(PublishedAt.Format)))
                                                             .WithUserProperty(Schema.Name, Encoding.UTF8.GetBytes(nameof(Payloads.ServiceProviderSetupSchemaPayload)))
                                                             .WithRetainFlag()
                                                             .Build();

                var republishInterval = TimeSpan.FromMinutes(1);
                var iterationCount = 0;

                _logger.LogWarning(">>> WAITING FOR SETUP SELECTION - Service provider startup is BLOCKED until selection is received on topic: {Topic}", setupSelectionTopic);

                // Initial subscription
                await _operationalClient.SubscribeAsync(subscribeOptions, loopCancellationToken);

                while (!tcs.Task.IsCompleted)
                {
                    loopCancellationToken.ThrowIfCancellationRequested();
                    iterationCount++;

                    try
                    {
                        await _operationalClient.PublishAsync(msg, loopCancellationToken);
                        _logger.LogWarning(">>> WAITING FOR SETUP SELECTION [{Iteration}] - Published setup schema on {Topic}, waiting for selection response...",
                                           iterationCount,
                                           setupSchemaTopic);

                        if (!tcs.Task.IsCompleted)
                        {
                            // Wait for either selection response or republish interval
                            await Task.WhenAny(tcs.Task, Task.Delay(republishInterval, loopCancellationToken));
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning(">>> SETUP SELECTION CANCELLED - Loop aborted due to disconnection or app shutdown after {Iterations} iteration(s)", iterationCount);
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                                           ">>> WAITING FOR SETUP SELECTION [{Iteration}] - Failed to publish setup schema, will retry in {Interval}",
                                           iterationCount,
                                           republishInterval);

                        try
                        {
                            await Task.Delay(republishInterval, loopCancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogWarning(">>> SETUP SELECTION CANCELLED - Loop aborted during retry delay after {Iterations} iteration(s)", iterationCount);
                            throw;
                        }
                    }
                }

                _logger.LogInformation("Setup selection received after {Iterations} iteration(s)", iterationCount);
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
                    _logger.LogWarning(ex, "Failed to unsubscribe from setup selection topic");
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
                var registrationAcceptedTopic = $"{Topics.ServiceProviderRegistrationAccepted}/{secret}";
                var registrationDeniedTopic = $"{Topics.ServiceProviderRegistrationDenied}/{secret}";

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

                var tcs = new TaskCompletionSource<Shared.Contracts.Events.MeshToServiceProvider.ServiceProviderRegistrationAcceptedPayload>();

                client.ApplicationMessageReceivedAsync += eventArgs =>
                                                          {
                                                              if (_logger.IsEnabled(LogLevel.Debug))
                                                              {
                                                                  _logger.LogDebug("Received registration message on topic {Topic}, content type {ContentType}, schema {Schema}",
                                                                                   eventArgs.ApplicationMessage.Topic,
                                                                                   eventArgs.ApplicationMessage.ContentType,
                                                                                   eventArgs.ApplicationMessage
                                                                                            .UserProperties
                                                                                            ?.FirstOrDefault(p => p.Name == Schema.Name)
                                                                                            ?.ReadValueAsString());
                                                              }

                                                              // Registration phase handling
                                                              if (eventArgs.ApplicationMessage.Topic == registrationAcceptedTopic)
                                                              {
                                                                  try
                                                                  {
                                                                      var acceptedPayload = JsonSerializer.Deserialize(eventArgs.ApplicationMessage.Payload.ToArray(),
                                                                                                                       JsonSerializationContexts.ServiceProviderJsonContext.Default
                                                                                                                           .ServiceProviderRegistrationAcceptedPayload);

                                                                      tcs.TrySetResult(acceptedPayload);
                                                                  }
                                                                  catch (Exception ex)
                                                                  {
                                                                      _logger.LogError(ex, "Failed to deserialize registration accepted payload");
                                                                  }

                                                                  return Task.CompletedTask;
                                                              }

                                                              _logger.LogError("Not expected registration message: topic {Topic}: {Reason}",
                                                                               eventArgs.ApplicationMessage.Topic,
                                                                               eventArgs.ResponseReasonString);

                                                              return Task.CompletedTask;
                                                          };
                client.DisconnectedAsync += e =>
                                            {
                                                _logger.LogWarning("mqtt client in registration disconnected: {ReasonString}(Reason: {Reason})", e.ReasonString, e.Reason);
                                                return Task.CompletedTask;
                                            };

                // publish registration
                var topic = $"{Topics.ServiceProviderRegistrationRequest}/{secret}";
                var payload = JsonSerializer.SerializeToUtf8Bytes(new ServiceProviderRegistrationRequestPayload(connectionData.ServiceProviderIdentifier),
                                                                  JsonSerializationContexts.ServiceProviderJsonContext.Default.ServiceProviderRegistrationRequestPayload);
                var msg = new MqttApplicationMessageBuilder().WithTopic(topic)
                                                             .WithPayload(payload)
                                                             .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                                                             .WithContentType(MessageMimeTypes.Json)
                                                             .WithCorrelationData(Guid.NewGuid().ToByteArray())
                                                             .WithUserProperty(PublishedAt.Name, Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString(PublishedAt.Format)))
                                                             .WithUserProperty(Schema.Name, Encoding.UTF8.GetBytes(nameof(ServiceProviderRegistrationRequestPayload)))
                                                             .WithRetainFlag()
                                                             .Build();

                var iterationCount = 0;
                var initialRetryInterval = TimeSpan.FromSeconds(30);
                var maxRetryInterval = TimeSpan.FromMinutes(30);
                var currentRetryInterval = initialRetryInterval;

                _logger.LogWarning(">>> WAITING FOR REGISTRATION ACCEPTANCE - Service provider startup is BLOCKED until registration is accepted on topic: {Topic}",
                                   registrationAcceptedTopic);

                while (!tcs.Task.IsCompleted)
                {
                    registrationToken.ThrowIfCancellationRequested();
                    iterationCount++;

                    try
                    {
                        if (!client.IsConnected)
                        {
                            await ConnectRegistrationClientAsync(connectionData, registrationOptions, mqttClientSubscribeOptions, client, registrationToken);
                        }

                        await client.PublishAsync(msg, registrationToken);

                        // Log strategy: verbose initially, then reduce frequency
                        // First 5 iterations: log every time
                        // Iterations 6-20: log every 5th
                        // After 20: log every 10th iteration OR every 30 minutes (whichever comes first)
                        var shouldLog = iterationCount <= 5 || (iterationCount <= 20 && iterationCount % 5 == 0) || (iterationCount > 20 && iterationCount % 10 == 0);

                        if (shouldLog)
                        {
                            var elapsed = TimeSpan.FromSeconds(30).Multiply(iterationCount - 1) + currentRetryInterval.Multiply(Math.Max(0, iterationCount - 1));
                            _logger.LogWarning(">>> WAITING FOR REGISTRATION [{Iteration}] - Published registration request (waiting ~{Elapsed}), next retry in {RetryInterval}",
                                               iterationCount,
                                               FormatElapsedTime(elapsed),
                                               currentRetryInterval);
                        }

                        if (!tcs.Task.IsCompleted)
                        {
                            // Wait for either registration response OR retry interval (whichever comes first)
                            await Task.WhenAny(tcs.Task, Task.Delay(currentRetryInterval, registrationToken));

                            // Only increase backoff if we're still waiting (not if response arrived)
                            if (!tcs.Task.IsCompleted && currentRetryInterval < maxRetryInterval)
                            {
                                currentRetryInterval = TimeSpan.FromMilliseconds(Math.Min(currentRetryInterval.TotalMilliseconds * 2, maxRetryInterval.TotalMilliseconds));
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning(">>> REGISTRATION CANCELLED - Loop aborted after {Iterations} iteration(s)", iterationCount);
                        throw;
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning(e,
                                           ">>> WAITING FOR REGISTRATION [{Iteration}] - Failed to publish, will retry in {RetryInterval}",
                                           iterationCount,
                                           currentRetryInterval);

                        try
                        {
                            // Wait for either registration response OR retry interval (whichever comes first)
                            await Task.WhenAny(tcs.Task, Task.Delay(currentRetryInterval, registrationToken));

                            // Only increase backoff on error if we're still waiting (not if response arrived)
                            if (!tcs.Task.IsCompleted && currentRetryInterval < maxRetryInterval)
                            {
                                currentRetryInterval = TimeSpan.FromMilliseconds(Math.Min(currentRetryInterval.TotalMilliseconds * 2, maxRetryInterval.TotalMilliseconds));
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogWarning(">>> REGISTRATION CANCELLED during retry delay after {Iterations} iteration(s)", iterationCount);
                            throw;
                        }
                    }
                }

                var acceptedPayload = await tcs.Task;

                _logger.LogInformation("Registration accepted after {Iterations} iteration(s) - received operational credentials", iterationCount);
                return new OperationalData(new MqttConnectionData(connectionData.ServiceProviderIdentifier, acceptedPayload.Host, acceptedPayload.Port),
                                           acceptedPayload.InstallationTopic,
                                           acceptedPayload.ClientId,
                                           acceptedPayload.Username,
                                           acceptedPayload.Password);
            }
            finally
            {
                if (client is { IsConnected: true })
                {
                    await client.DisconnectAsync(reasonString: "Registration done or canceled", cancellationToken: CancellationToken.None);
                }
            }
        }

        private static string FormatElapsedTime(TimeSpan elapsed)
        {
            if (elapsed.TotalDays >= 1)
            {
                return $"{elapsed.TotalDays:F1} days";
            }

            if (elapsed.TotalHours >= 1)
            {
                return $"{elapsed.TotalHours:F1} hours";
            }

            if (elapsed.TotalMinutes >= 1)
            {
                return $"{elapsed.TotalMinutes:F0} minutes";
            }

            return $"{elapsed.TotalSeconds:F0} seconds";
        }

        private async Task ConnectRegistrationClientAsync(MqttConnectionData connectionData,
                                                          MqttClientOptions registrationOptions,
                                                          MqttClientSubscribeOptions mqttClientSubscribeOptions,
                                                          IMqttClient client,
                                                          CancellationToken ct)
        {
            _logger.LogInformation("Connecting for registration to MQTT broker at {Host}:{Port}...", connectionData.Host, connectionData.Port);
            var result = await client.ConnectAsync(registrationOptions, ct);
            _logger.LogInformation("Connected for registration:{Reason} ({ResultCode})", result.ReasonString, result.ResultCode);

            var x = await client.SubscribeAsync(mqttClientSubscribeOptions, ct);
            _logger.LogInformation("Subscribed for registration:{Reason} ({Items})",
                                   x.ReasonString,
                                   string.Join(", ", x.Items.Select(i => $"{i.TopicFilter.Topic}: {i.ResultCode}")));
        }

        private async Task SendDeclarationAsync(OperationalData operationalData, ServiceProviderDeclarationPayload declaration, CancellationToken cancellationToken)
        {
            var installationTopic = operationalData.InstallationTopic;
            var payload = JsonSerializer.SerializeToUtf8Bytes(declaration, JsonSerializationContexts.ServiceProviderJsonContext.Default.ServiceProviderDeclarationPayload);
            var topic = $"{installationTopic}/{operationalData.ConnectionData.ServiceProviderIdentifier}{Topics.ServiceProviderDeclaration}";
            var msg = new MqttApplicationMessageBuilder().WithTopic(topic)
                                                         .WithPayload(payload)
                                                         .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                                                         .WithContentType(MessageMimeTypes.Json)
                                                         .WithCorrelationData(Guid.NewGuid().ToByteArray())
                                                         .WithUserProperty(PublishedAt.Name, Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString(PublishedAt.Format)))
                                                         .WithUserProperty(Schema.Name, Encoding.UTF8.GetBytes(nameof(ServiceProviderDeclarationPayload)))
                                                         .WithRetainFlag()
                                                         .Build();

            //try
            {
                await _operationalClient.PublishAsync(msg, cancellationToken);
                _logger.LogInformation("Published service provider declaration on {Topic}", topic);
            }

            //catch (Exception ex)
            {
                //_logger.LogWarning(ex, "Failed publishing declaration on {Topic}", topic);
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
                _logger.LogDebug(e, "CancellationTokenSource {Name} already disposed", name);
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
                    _logger.LogDebug(e, "Failed to cancel CancellationTokenSource {Name}", name);
                }

                try
                {
                    oldCts.Dispose();
                }
                catch (Exception e)
                {
                    _logger.LogDebug(e, "Failed to dispose CancellationTokenSource {Name}", name);
                }
            }
        }

        #endregion flow
    }
}