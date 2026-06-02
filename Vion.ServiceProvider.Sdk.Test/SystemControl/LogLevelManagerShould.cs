using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vion.ServiceProvider.Sdk.SystemControl;

namespace Vion.ServiceProvider.Sdk.Test.SystemControl
{
    [TestClass]
    public class LogLevelManagerShould
    {
        private const string LogLevelKey = "Logging:LogLevel:Default";

        [TestInitialize]
        public void Initialize()
        {
            LogLevelManager.CurrentLevel = LogLevel.Information;
        }

        [TestMethod]
        public void ApplyLogLevelFromConfiguration()
        {
            // Arrange
            var configuration = BuildConfiguration(new Dictionary<string, string?> { [LogLevelKey] = "Debug" });

            // Act
            LogLevelManager.InitializeFromConfig(configuration);

            // Assert
            Assert.AreEqual(LogLevel.Debug, LogLevelManager.CurrentLevel);
        }

        [TestMethod]
        public void ParseLogLevelCaseInsensitively()
        {
            // Arrange
            var configuration = BuildConfiguration(new Dictionary<string, string?> { [LogLevelKey] = "warning" });

            // Act
            LogLevelManager.InitializeFromConfig(configuration);

            // Assert
            Assert.AreEqual(LogLevel.Warning, LogLevelManager.CurrentLevel);
        }

        [TestMethod]
        public void IgnoreMissingConfigKey()
        {
            // Arrange
            LogLevelManager.CurrentLevel = LogLevel.Critical;
            var configuration = BuildConfiguration(new Dictionary<string, string?>());

            // Act
            LogLevelManager.InitializeFromConfig(configuration);

            // Assert
            Assert.AreEqual(LogLevel.Critical, LogLevelManager.CurrentLevel);
        }

        [TestMethod]
        public void IgnoreInvalidConfigValue()
        {
            // Arrange
            LogLevelManager.CurrentLevel = LogLevel.Critical;
            var configuration = BuildConfiguration(new Dictionary<string, string?> { [LogLevelKey] = "Bogus" });

            // Act
            LogLevelManager.InitializeFromConfig(configuration);

            // Assert
            Assert.AreEqual(LogLevel.Critical, LogLevelManager.CurrentLevel);
        }

        private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
        {
            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }
    }
}