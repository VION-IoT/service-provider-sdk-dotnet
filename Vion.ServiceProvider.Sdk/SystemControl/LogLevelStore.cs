using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Vion.ServiceProvider.Sdk.Infrastructure;

namespace Vion.ServiceProvider.Sdk.SystemControl
{
    /// <summary>
    ///     Persists the current log level as plain text to a file, so a cloud-set level survives a restart.
    /// </summary>
    public sealed class LogLevelStore : ILogLevelStore
    {
        private readonly IDiskAccessProvider _diskAccessProvider;

        private readonly string _filePath;

        /// <summary>Initializes a new instance of the <see cref="LogLevelStore" /> class.</summary>
        /// <param name="diskAccessProvider">The disk access used to read and persist the level.</param>
        /// <param name="filePath">The file path the level is persisted to.</param>
        public LogLevelStore(IDiskAccessProvider diskAccessProvider, string filePath)
        {
            _diskAccessProvider = diskAccessProvider;
            _filePath = filePath;
        }

        /// <inheritdoc />
        public LogLevel? Read()
        {
            return _diskAccessProvider.FileExists(_filePath) && Enum.TryParse<LogLevel>(_diskAccessProvider.ReadAllText(_filePath), true, out var logLevel) ? logLevel : null;
        }

        /// <inheritdoc />
        public void Write(LogLevel logLevel)
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                _diskAccessProvider.CreateDirectory(directory);
            }

            _diskAccessProvider.WriteAllText(_filePath, logLevel.ToString());
        }
    }
}
