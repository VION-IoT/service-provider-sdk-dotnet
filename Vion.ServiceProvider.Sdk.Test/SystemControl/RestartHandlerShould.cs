using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Vion.ServiceProvider.Sdk.RegistrationFlow;
using Vion.ServiceProvider.Sdk.SystemControl;
using Vion.ServiceProvider.Sdk.Test.TestHelpers;

namespace Vion.ServiceProvider.Sdk.Test.SystemControl
{
    [TestClass]
    public class RestartHandlerShould
    {
        private readonly Mock<IHostApplicationLifetime> _lifetimeMock = new();

        private readonly Mock<ILogger<RestartHandler>> _loggerMock = new();

        private readonly TimeSpan _testTimeout = TimeSpan.FromSeconds(5);

        private RestartHandler _sut = null!;

        [TestInitialize]
        public void Initialize()
        {
            _sut = new RestartHandler(_lifetimeMock.Object, _loggerMock.Object);
        }

        [TestMethod]
        public async Task StopTheApplication()
        {
            // Arrange
            var stopRequested = new TaskCompletionSource();
            _lifetimeMock.Setup(lifetime => lifetime.StopApplication()).Callback(() => stopRequested.TrySetResult());
            var message = MqttApplicationMessageBuilder.BuildEmptyPayload(Guid.NewGuid().ToString());

            // Act
            await _sut.HandleAsync(Mock.Of<IServiceProviderPublish>(), message, Guid.NewGuid(), CancellationToken.None);
            await stopRequested.Task.WaitAsync(_testTimeout, CancellationToken.None);

            // Assert
            _lifetimeMock.Verify(lifetime => lifetime.StopApplication(), Times.Once);
        }

        [TestMethod]
        public void ReturnWithoutWaitingForStop()
        {
            // Arrange
            // ReSharper disable once AsyncApostle.AsyncWait
            _lifetimeMock.Setup(lifetime => lifetime.StopApplication()).Callback(() => new TaskCompletionSource().Task.Wait(CancellationToken.None));
            var message = MqttApplicationMessageBuilder.BuildEmptyPayload(Guid.NewGuid().ToString());

            // Act
            var handleTask = _sut.HandleAsync(Mock.Of<IServiceProviderPublish>(), message, Guid.NewGuid(), CancellationToken.None);

            // Assert
            Assert.IsTrue(handleTask.IsCompletedSuccessfully);
        }
    }
}