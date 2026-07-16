using Microsoft.Extensions.Logging;

namespace Vion.ServiceProvider.Sdk.SystemControl
{
    /// <summary>
    ///     Persists the service provider's log level across restarts.
    /// </summary>
    public interface ILogLevelStore
    {
        /// <summary>
        ///     Returns the persisted log level, or <c>null</c> if none has been persisted yet (first boot or after data loss) or
        ///     the stored value is not a recognised <see cref="LogLevel" />.
        /// </summary>
        LogLevel? Read();

        /// <summary>
        ///     Persists <paramref name="logLevel" />, replacing any previous value.
        /// </summary>
        /// <param name="logLevel">The log level to persist.</param>
        void Write(LogLevel logLevel);
    }
}
