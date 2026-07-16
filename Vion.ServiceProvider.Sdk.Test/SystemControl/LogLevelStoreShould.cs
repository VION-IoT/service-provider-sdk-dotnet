using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Moq;
using Vion.ServiceProvider.Sdk.Infrastructure;
using Vion.ServiceProvider.Sdk.SystemControl;

namespace Vion.ServiceProvider.Sdk.Test.SystemControl
{
    [TestClass]
    public class LogLevelStoreShould
    {
        private const string FilePath = "/data/logLevel.txt";

        private readonly Mock<IDiskAccessProvider> _diskAccessProviderMock = new();

        private readonly Dictionary<string, string> _diskState = new();

        private LogLevelStore _sut = null!;

        [TestInitialize]
        public void Initialize()
        {
            _diskAccessProviderMock.Setup(disk => disk.FileExists(It.IsAny<string>())).Returns<string>(path => _diskState.ContainsKey(path));
            _diskAccessProviderMock.Setup(disk => disk.ReadAllText(It.IsAny<string>())).Returns<string>(path => _diskState[path]);
            _diskAccessProviderMock.Setup(disk => disk.WriteAllText(It.IsAny<string>(), It.IsAny<string>()))
                                   .Callback<string, string>((path, contents) => _diskState[path] = contents);
            _sut = new LogLevelStore(_diskAccessProviderMock.Object, FilePath);
        }

        [DataRow("Warning", LogLevel.Warning, DisplayName = "Canonical casing")]
        [DataRow("debug", LogLevel.Debug, DisplayName = "Lower casing")]
        [TestMethod]
        public void ReturnPersistedLevelWhenFileExists(string storedValue, LogLevel expectedLevel)
        {
            // Arrange
            _diskState[FilePath] = storedValue;

            // Act
            var persistedLevel = _sut.Read();

            // Assert
            Assert.AreEqual(expectedLevel, persistedLevel);
        }

        [TestMethod]
        public void ReturnNullWhenFileAbsent()
        {
            // Arrange

            // Act
            var persistedLevel = _sut.Read();

            // Assert
            Assert.IsNull(persistedLevel);
        }

        [TestMethod]
        public void ReturnNullWhenPersistedValueUnparseable()
        {
            // Arrange
            _diskState[FilePath] = "not-a-log-level";

            // Act
            var persistedLevel = _sut.Read();

            // Assert
            Assert.IsNull(persistedLevel);
        }

        [DataRow(LogLevel.Debug, "Debug")]
        [DataRow(LogLevel.Warning, "Warning")]
        [TestMethod]
        public void PersistLevelToFile(LogLevel logLevel, string expectedContent)
        {
            // Arrange

            // Act
            _sut.Write(logLevel);

            // Assert
            Assert.AreEqual(expectedContent, _diskState[FilePath]);
        }

        [TestMethod]
        public void CreateDirectoryWhenPersisting()
        {
            // Arrange

            // Act
            _sut.Write(LogLevel.Warning);

            // Assert
            _diskAccessProviderMock.Verify(disk => disk.CreateDirectory(Path.GetDirectoryName(FilePath)!), Times.Once);
        }
    }
}
