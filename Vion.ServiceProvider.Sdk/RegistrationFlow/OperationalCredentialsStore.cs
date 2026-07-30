using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vion.ServiceProvider.Sdk.Infrastructure;
using Vion.ServiceProvider.Sdk.JsonSerializationContexts;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    /// <inheritdoc />
    public sealed partial class OperationalCredentialsStore : IOperationalCredentialsStore
    {
        private readonly IDiskAccessProvider _diskAccessProvider;

        private readonly string _filePath;

        private readonly ILogger _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="OperationalCredentialsStore" /> class.
        /// </summary>
        /// <param name="diskAccessProvider">The disk access used to read and persist the credentials.</param>
        /// <param name="filePath">The file path the credentials are persisted to.</param>
        /// <param name="logger">The logger.</param>
        public OperationalCredentialsStore(IDiskAccessProvider diskAccessProvider, string filePath, ILogger logger)
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
                    LogCredentialsUnreadable(_filePath);
                    return null;
                }

                LogCredentialsLoaded(_filePath);
                return operationalData;
            }
            catch (Exception exception)
            {
                LogCredentialsReadFailed(exception, _filePath);
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
                LogCredentialsWriteFailed(exception, _filePath);
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
                LogCredentialsClearFailed(exception, _filePath);
            }
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "Loaded stored operational credentials from '{Path}'")]
        private partial void LogCredentialsLoaded(string path);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Stored operational credentials at '{Path}' could not be read back")]
        private partial void LogCredentialsUnreadable(string path);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to read stored operational credentials from '{Path}'")]
        private partial void LogCredentialsReadFailed(Exception exception, string path);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to store operational credentials at '{Path}'")]
        private partial void LogCredentialsWriteFailed(Exception exception, string path);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to remove stored operational credentials at '{Path}'")]
        private partial void LogCredentialsClearFailed(Exception exception, string path);
    }
}
