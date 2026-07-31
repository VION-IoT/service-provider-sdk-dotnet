using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vion.ServiceProvider.Sdk.Infrastructure;
using Vion.ServiceProvider.Sdk.JsonSerializationContexts;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    /// <inheritdoc />
    public sealed partial class OperationalMqttDataStore : IOperationalMqttDataStore
    {
        private readonly IDiskAccessProvider _diskAccessProvider;

        private readonly string _filePath;

        private readonly ILogger _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="OperationalMqttDataStore" /> class.
        /// </summary>
        /// <param name="diskAccessProvider">The disk access used to read and persist the data.</param>
        /// <param name="filePath">The file path the data is persisted to.</param>
        /// <param name="logger">The logger.</param>
        public OperationalMqttDataStore(IDiskAccessProvider diskAccessProvider, string filePath, ILogger logger)
        {
            _diskAccessProvider = diskAccessProvider;
            _filePath = filePath;
            _logger = logger;
        }

        /// <inheritdoc />
        public OperationalData? Read()
        {
            try
            {
                if (!_diskAccessProvider.FileExists(_filePath))
                {
                    return null;
                }

                var operationalData = JsonSerializer.Deserialize(_diskAccessProvider.ReadAllText(_filePath), ServiceProviderJsonContext.Default.OperationalData);
                if (operationalData == null)
                {
                    LogDataUnreadable(_filePath);
                    return null;
                }

                LogDataLoaded(_filePath);
                return operationalData;
            }
            catch (Exception exception)
            {
                LogDataReadFailed(exception, _filePath);
                return null;
            }
        }

        /// <inheritdoc />
        public void Write(OperationalData operationalData)
        {
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    _diskAccessProvider.CreateDirectory(directory);
                }

                var temporaryPath = _filePath + ".tmp";
                _diskAccessProvider.WriteAllText(temporaryPath, JsonSerializer.Serialize(operationalData, ServiceProviderJsonContext.Default.OperationalData));
                _diskAccessProvider.MoveFile(temporaryPath, _filePath, true);
            }
            catch (Exception exception)
            {
                LogDataWriteFailed(exception, _filePath);
            }
        }

        /// <inheritdoc />
        public void Clear()
        {
            try
            {
                _diskAccessProvider.DeleteFile(_filePath);
            }
            catch (Exception exception)
            {
                LogDataClearFailed(exception, _filePath);
            }
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "Loaded stored operational MQTT data from '{Path}'")]
        private partial void LogDataLoaded(string path);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Stored operational MQTT data at '{Path}' could not be read back")]
        private partial void LogDataUnreadable(string path);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to read stored operational MQTT data from '{Path}'")]
        private partial void LogDataReadFailed(Exception exception, string path);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to store operational MQTT data at '{Path}'")]
        private partial void LogDataWriteFailed(Exception exception, string path);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to remove stored operational MQTT data at '{Path}'")]
        private partial void LogDataClearFailed(Exception exception, string path);
    }
}
