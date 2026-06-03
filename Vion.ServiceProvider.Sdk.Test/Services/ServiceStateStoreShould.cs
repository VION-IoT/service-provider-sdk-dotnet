using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Vion.ServiceProvider.Sdk.Infrastructure;
using Vion.ServiceProvider.Sdk.Services;
using Vion.ServiceProvider.Sdk.Test.TestHelpers;

namespace Vion.ServiceProvider.Sdk.Test.Services
{
    [TestClass]
    public class ServiceStateStoreShould
    {
        private const string StateFilePath = "/data/state.json";

        private readonly Mock<IDiskAccessProvider> _diskAccessProviderMock = new();

        private readonly Dictionary<string, string> _diskState = new();

        private readonly Mock<ILogger<ServiceStateStore<TestServiceState>>> _loggerMock = new();

        private readonly TestSchema _schema = new();

        private readonly TimeSpan _testTimeout = TimeSpan.FromSeconds(5);

        private ServiceStateStore<TestServiceState> _sut = null!;

        [TestInitialize]
        public void Initialize()
        {
            _diskAccessProviderMock.Setup(disk => disk.FileExists(It.IsAny<string>())).Returns<string>(path => _diskState.ContainsKey(path));
            _diskAccessProviderMock.Setup(disk => disk.ReadAllText(It.IsAny<string>())).Returns<string>(path => _diskState[path]);
            _diskAccessProviderMock.Setup(disk => disk.WriteAllText(It.IsAny<string>(), It.IsAny<string>()))
                                   .Callback<string, string>((path, contents) => _diskState[path] = contents);

            _sut = new ServiceStateStore<TestServiceState>(_diskAccessProviderMock.Object,
                                                           StateFilePath,
                                                           _schema,
                                                           TestServiceStateJsonContext.Default.TestServiceState,
                                                           new TestServiceState(),
                                                           _loggerMock.Object);
        }

        [TestMethod]
        public async Task LoadPersistedStateWhenInitialized()
        {
            // Arrange
            var expectedPlain = Guid.NewGuid().ToString();
            var expectedSecret = Guid.NewGuid().ToString();
            _diskState[StateFilePath] = $"{{\"plain\":\"{expectedPlain}\",\"secret\":\"{expectedSecret}\"}}";

            // Act
            var loaded = await _sut.InitializeAsync(CancellationToken.None).WaitAsync(_testTimeout, CancellationToken.None);

            // Assert
            Assert.AreEqual(expectedPlain, loaded.Plain);
            Assert.AreEqual(expectedSecret, loaded.Secret);
        }

        [TestMethod]
        public async Task StartWithDefaultStateWhenNothingPersisted()
        {
            // Arrange

            // Act
            var loaded = await _sut.InitializeAsync(CancellationToken.None).WaitAsync(_testTimeout, CancellationToken.None);

            // Assert
            Assert.AreEqual("", loaded.Plain);
            Assert.IsNull(loaded.Secret);
            Assert.AreEqual(0d, loaded.Reading);
        }

        [TestMethod]
        public async Task NotifySubscribersOfStateChangeOnInitialize()
        {
            // Arrange
            var raisedCount = 0;
            _sut.StateChanged += _ =>
                                 {
                                     raisedCount++;
                                     return Task.CompletedTask;
                                 };

            // Act
            await _sut.InitializeAsync(CancellationToken.None).WaitAsync(_testTimeout, CancellationToken.None);

            // Assert
            Assert.AreEqual(1, raisedCount);
        }

        [TestMethod]
        public async Task NotifyOtherSubscribersWhenOneThrows()
        {
            // Arrange
            var notified = false;
            _sut.StateChanged += _ => throw new InvalidOperationException();
            _sut.StateChanged += _ =>
                                 {
                                     notified = true;
                                     return Task.CompletedTask;
                                 };

            // Act
            await _sut.InitializeAsync(CancellationToken.None).WaitAsync(_testTimeout, CancellationToken.None);

            // Assert
            Assert.IsTrue(notified);
        }

        [TestMethod]
        public async Task ThrowOnUpdateOfUnknownField()
        {
            // Arrange
            await _sut.InitializeAsync(CancellationToken.None).WaitAsync(_testTimeout, CancellationToken.None);

            // Act / Assert
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => _sut.UpdateAsync(Guid.NewGuid().ToString(),
                                                                                      JsonValue.Create(Guid.NewGuid().ToString()),
                                                                                      CancellationToken.None));
        }

        [TestMethod]
        public async Task ThrowOnUpdateWhenNotInitialized()
        {
            // Arrange

            // Act / Assert
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _sut.UpdateAsync(TestSchema.Plain.Name,
                                                                                              JsonValue.Create(Guid.NewGuid().ToString()),
                                                                                              CancellationToken.None));
        }

        [TestMethod]
        public async Task CreateStateDirectoryWhenPersisting()
        {
            // Arrange
            await _sut.InitializeAsync(CancellationToken.None).WaitAsync(_testTimeout, CancellationToken.None);

            // Act
            await _sut.UpdateAsync(TestSchema.Plain.Name, null, CancellationToken.None).WaitAsync(_testTimeout, CancellationToken.None);

            // Assert
            _diskAccessProviderMock.Verify(disk => disk.CreateDirectory(Path.GetDirectoryName(StateFilePath)!), Times.Once);
        }

        [TestMethod]
        public async Task PersistChangedState()
        {
            // Arrange
            await _sut.InitializeAsync(CancellationToken.None).WaitAsync(_testTimeout, CancellationToken.None);
            var expectedPlain = Guid.NewGuid().ToString();

            // Act
            await _sut.UpdateAsync(TestSchema.Plain.Name, JsonValue.Create(expectedPlain), CancellationToken.None).WaitAsync(_testTimeout, CancellationToken.None);

            // Assert
            Assert.Contains(expectedPlain, _diskState[StateFilePath]);
        }

        [TestMethod]
        public async Task NotifySubscribersOfStateChangeOnUpdate()
        {
            // Arrange
            await _sut.InitializeAsync(CancellationToken.None).WaitAsync(_testTimeout, CancellationToken.None);
            TestServiceState? capturedState = null;
            _sut.StateChanged += state =>
                                 {
                                     capturedState = state;
                                     return Task.CompletedTask;
                                 };
            var expectedReading = Random.Shared.NextDouble();

            // Act
            await _sut.UpdateAsync(TestSchema.Reading.Name, JsonValue.Create(expectedReading), CancellationToken.None).WaitAsync(_testTimeout, CancellationToken.None);

            // Assert
            Assert.IsNotNull(capturedState);
            Assert.AreEqual(expectedReading, capturedState.Reading);
        }

        [TestMethod]
        public async Task ReturnUpdatedState()
        {
            // Arrange
            await _sut.InitializeAsync(CancellationToken.None).WaitAsync(_testTimeout, CancellationToken.None);
            var expectedPlain = Guid.NewGuid().ToString();

            // Act
            var updated = await _sut.UpdateAsync(TestSchema.Plain.Name, JsonValue.Create(expectedPlain), CancellationToken.None).WaitAsync(_testTimeout, CancellationToken.None);

            // Assert
            Assert.AreEqual(expectedPlain, updated.Plain);
        }

        [TestMethod]
        public async Task ThrowOnGetCurrentWhenNotInitialized()
        {
            // Arrange

            // Act / Assert
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _sut.GetCurrentAsync(CancellationToken.None));
        }

        [TestMethod]
        public async Task ReturnCurrentState()
        {
            // Arrange
            var expectedPlain = Guid.NewGuid().ToString();
            var expectedSecret = Guid.NewGuid().ToString();
            _diskState[StateFilePath] = $"{{\"plain\":\"{expectedPlain}\",\"secret\":\"{expectedSecret}\"}}";
            await _sut.InitializeAsync(CancellationToken.None).WaitAsync(_testTimeout, CancellationToken.None);

            // Act
            var current = await _sut.GetCurrentAsync(CancellationToken.None).WaitAsync(_testTimeout, CancellationToken.None);

            // Assert
            Assert.AreEqual(expectedPlain, current.Plain);
            Assert.AreEqual(expectedSecret, current.Secret);
        }

        [TestMethod]
        public async Task ReturnCurrentStateAfterUpdate()
        {
            // Arrange
            await _sut.InitializeAsync(CancellationToken.None).WaitAsync(_testTimeout, CancellationToken.None);
            var expectedPlain = Guid.NewGuid().ToString();
            await _sut.UpdateAsync(TestSchema.Plain.Name, JsonValue.Create(expectedPlain), CancellationToken.None).WaitAsync(_testTimeout, CancellationToken.None);

            // Act
            var current = await _sut.GetCurrentAsync(CancellationToken.None).WaitAsync(_testTimeout, CancellationToken.None);

            // Assert
            Assert.AreEqual(expectedPlain, current.Plain);
        }
    }
}