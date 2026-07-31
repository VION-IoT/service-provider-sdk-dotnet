using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using Moq;
using Vion.ServiceProvider.Sdk.Infrastructure;
using Vion.ServiceProvider.Sdk.RegistrationFlow;

namespace Vion.ServiceProvider.Sdk.Test.RegistrationFlow
{
    [TestClass]
    public class OperationalMqttDataStoreShould
    {
        private const string FilePath = "/data/operationalMqttData.json";

        private const string TemporaryFilePath = FilePath + ".tmp";

        private readonly Mock<IDiskAccessProvider> _diskAccessProviderMock = new();

        private readonly Dictionary<string, string> _diskState = new();

        private readonly OperationalData _operationalData = new(new MqttConnectionData("a-service-provider", "broker", 1883),
                                                                "vion/tenant/gateway",
                                                                "a-service-provider",
                                                                "a-service-provider",
                                                                "an-operational-password");

        private OperationalMqttDataStore _sut = null!;

        [TestInitialize]
        public void Initialize()
        {
            _diskAccessProviderMock.Setup(disk => disk.FileExists(It.IsAny<string>())).Returns<string>(path => _diskState.ContainsKey(path));
            _diskAccessProviderMock.Setup(disk => disk.ReadAllText(It.IsAny<string>())).Returns<string>(path => _diskState[path]);
            _diskAccessProviderMock.Setup(disk => disk.WriteAllText(It.IsAny<string>(), It.IsAny<string>()))
                                   .Callback<string, string>((path, contents) => _diskState[path] = contents);
            _diskAccessProviderMock.Setup(disk => disk.MoveFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                                   .Callback<string, string, bool>((sourcePath, destinationPath, _) =>
                                                                   {
                                                                       _diskState[destinationPath] = _diskState[sourcePath];
                                                                       _diskState.Remove(sourcePath);
                                                                   });
            _diskAccessProviderMock.Setup(disk => disk.DeleteFile(It.IsAny<string>())).Callback<string>(path => _diskState.Remove(path));
            _sut = new OperationalMqttDataStore(_diskAccessProviderMock.Object, FilePath, Mock.Of<ILogger>());
        }

        [TestMethod]
        public void ReturnPersistedCredentialsWhenFileExists()
        {
            // Arrange
            _sut.Write(_operationalData);

            // Act
            var persistedData = _sut.Read();

            // Assert
            Assert.AreEqual(_operationalData, persistedData);
        }

        [TestMethod]
        public void ReturnNullWhenFileAbsent()
        {
            // Arrange

            // Act
            var persistedData = _sut.Read();

            // Assert
            Assert.IsNull(persistedData);
        }

        [TestMethod]
        public void ReturnNullWhenPersistedContentUnparseable()
        {
            // Arrange — a torn or corrupted file must read as "nothing stored" rather than throwing.
            _diskState[FilePath] = "{ not json";

            // Act
            var persistedData = _sut.Read();

            // Assert
            Assert.IsNull(persistedData);
        }

        [TestMethod]
        public void ReplaceTheTargetByRenamingSoAnInterruptedWriteCannotBeObserved()
        {
            // Arrange

            // Act
            _sut.Write(_operationalData);

            // Assert
            _diskAccessProviderMock.Verify(disk => disk.WriteAllText(TemporaryFilePath, It.IsAny<string>()), Times.Once);
            _diskAccessProviderMock.Verify(disk => disk.MoveFile(TemporaryFilePath, FilePath, true), Times.Once);
            _diskAccessProviderMock.Verify(disk => disk.WriteAllText(FilePath, It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public void CreateDirectoryWhenPersisting()
        {
            // Arrange

            // Act
            _sut.Write(_operationalData);

            // Assert
            _diskAccessProviderMock.Verify(disk => disk.CreateDirectory(Path.GetDirectoryName(FilePath)!), Times.Once);
        }

        [TestMethod]
        public void ReturnNullAfterClearing()
        {
            // Arrange
            _sut.Write(_operationalData);

            // Act
            _sut.Clear();

            // Assert
            Assert.IsNull(_sut.Read());
        }

        [TestMethod]
        public void SurviveAFailedClear()
        {
            // Arrange
            _diskAccessProviderMock.Setup(disk => disk.DeleteFile(It.IsAny<string>())).Throws(new IOException("no such file"));

            // Act
            _sut.Clear();

            // Assert — a failed clear must not propagate to the caller.
            _diskAccessProviderMock.Verify(disk => disk.DeleteFile(FilePath), Times.Once);
        }

        [TestMethod]
        public void SurviveAFailedWrite()
        {
            // Arrange
            _diskAccessProviderMock.Setup(disk => disk.WriteAllText(It.IsAny<string>(), It.IsAny<string>())).Throws(new UnauthorizedAccessException("read-only volume"));

            // Act
            _sut.Write(_operationalData);

            // Assert — a failed write must not propagate, and must leave nothing stored.
            Assert.IsNull(_sut.Read());
        }
    }
}
