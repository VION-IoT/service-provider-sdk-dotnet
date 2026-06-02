using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Vion.ServiceProvider.Sdk.Infrastructure;

namespace Vion.ServiceProvider.Sdk.Services
{
    /// <summary>
    ///     Owns a service's state, persisted as source-generated JSON to a file. Updates apply a single field through the
    ///     schema, persist, and raise <see cref="StateChanged" />.
    /// </summary>
    /// <typeparam name="TService">The service type whose state is managed.</typeparam>
    public sealed partial class ServiceStateStore<TService> : IServiceStateStore<TService>, IDisposable
    {
        private readonly IDiskAccessProvider _diskAccessProvider;

        private readonly ILogger<ServiceStateStore<TService>> _logger;

        private readonly ServiceSchema<TService> _schema;

        private readonly string _stateFilePath;

        private readonly SemaphoreSlim _stateLock = new(1, 1);

        private readonly JsonTypeInfo<TService> _typeInfo;

        private TService _current;

        private bool _initialized;

        /// <summary>Initializes a new instance of the <see cref="ServiceStateStore{TService}" /> class.</summary>
        /// <param name="diskAccessProvider">The disk access used to load and persist state.</param>
        /// <param name="stateFilePath">The file path the state is persisted to.</param>
        /// <param name="schema">The schema describing the service's fields.</param>
        /// <param name="typeInfo">The source-generated JSON type metadata for <typeparamref name="TService" />.</param>
        /// <param name="defaultState">The state to start from when nothing is persisted.</param>
        /// <param name="logger">The logger.</param>
        public ServiceStateStore(IDiskAccessProvider diskAccessProvider,
                                 string stateFilePath,
                                 ServiceSchema<TService> schema,
                                 JsonTypeInfo<TService> typeInfo,
                                 TService defaultState,
                                 ILogger<ServiceStateStore<TService>> logger)
        {
            _diskAccessProvider = diskAccessProvider;
            _stateFilePath = stateFilePath;
            _schema = schema;
            _typeInfo = typeInfo;
            _current = defaultState;
            _logger = logger;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _stateLock.Dispose();
        }

        /// <inheritdoc />
        public event Func<TService, Task>? StateChanged;

        /// <inheritdoc />
        public async Task<TService> InitializeAsync(CancellationToken cancellationToken)
        {
            TService snapshot;
            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                if (_initialized)
                {
                    return _current;
                }

                _initialized = true;
                if (!_diskAccessProvider.FileExists(_stateFilePath))
                {
                    LogStateFileMissing(_stateFilePath);
                }
                else
                {
                    var json = _diskAccessProvider.ReadAllText(_stateFilePath);
                    var loaded = JsonSerializer.Deserialize(json, _typeInfo);
                    if (loaded != null)
                    {
                        _current = loaded;
                        LogLoadedState(_stateFilePath);
                    }
                    else
                    {
                        LogStateFileEmpty(_stateFilePath);
                    }
                }

                snapshot = _current;
            }
            finally
            {
                _stateLock.Release();
            }

            await RaiseStateChangedAsync(snapshot);

            return snapshot;
        }

        /// <inheritdoc />
        public async Task<TService> UpdateAsync(string field, JsonNode? value, CancellationToken cancellationToken)
        {
            if (!_schema.TryGet(field, out var serviceField))
            {
                throw new ArgumentException($"Unknown field '{field}'.", nameof(field));
            }

            TService newState;
            await _stateLock.WaitAsync(cancellationToken);
            try
            {
                if (!_initialized)
                {
                    throw new InvalidOperationException($"{nameof(ServiceStateStore<>)} must be initialized before {nameof(UpdateAsync)}.");
                }

                newState = serviceField.WriteTo(_current, value);
                Persist(newState);
                _current = newState;
                LogUpdatedState(field);
            }
            finally
            {
                _stateLock.Release();
            }

            await RaiseStateChangedAsync(newState);
            return newState;
        }

        private void Persist(TService state)
        {
            var directory = Path.GetDirectoryName(_stateFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                _diskAccessProvider.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(state, _typeInfo);
            _diskAccessProvider.WriteAllText(_stateFilePath, json);
        }

        private async Task RaiseStateChangedAsync(TService state)
        {
            var handler = StateChanged;
            if (handler == null)
            {
                return;
            }

            foreach (var subscriber in handler.GetInvocationList())
            {
                try
                {
                    await ((Func<TService, Task>)subscriber).Invoke(state);
                }
                catch (Exception exception)
                {
                    LogSubscriberFailed(exception);
                }
            }
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "Loaded persisted state from '{Path}'")]
        private partial void LogLoadedState(string path);

        [LoggerMessage(Level = LogLevel.Information, Message = "Persisted state file '{Path}' is empty — starting with default state")]
        private partial void LogStateFileEmpty(string path);

        [LoggerMessage(Level = LogLevel.Information, Message = "No persisted state file at '{Path}' — starting with default state")]
        private partial void LogStateFileMissing(string path);

        [LoggerMessage(Level = LogLevel.Information, Message = "State updated (Field={Field})")]
        private partial void LogUpdatedState(string field);

        [LoggerMessage(Level = LogLevel.Error, Message = "State-changed subscriber threw")]
        private partial void LogSubscriberFailed(Exception exception);
    }
}