using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Vion.ServiceProvider.Sdk.SystemControl
{
    /// <summary>
    ///     Holds the service provider's current log level.
    /// </summary>
    public static class LogLevelManager
    {
        private static int _currentLevel = (int)LogLevel.Information;

        /// <summary>
        ///     Gets or sets the current log level. Reads and writes are atomic.
        /// </summary>
        public static LogLevel CurrentLevel
        {
            get => (LogLevel)_currentLevel;

            set => _currentLevel = (int)value;
        }

        /// <summary>
        ///     Initializes <see cref="CurrentLevel" /> from the configuration value, if present and parseable. Leaves the current
        ///     value unchanged otherwise.
        /// </summary>
        /// <param name="configuration">The configuration to read the default log level from.</param>
        public static void InitializeFromConfig(IConfiguration configuration)
        {
            var logLevelString = configuration["Logging:LogLevel:Default"];
            if (Enum.TryParse<LogLevel>(logLevelString, true, out var logLevel))
            {
                CurrentLevel = logLevel;
            }
        }
    }
}
