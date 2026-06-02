using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Vion.Contracts.Events.CloudToMesh;
using Vion.ServiceProvider.Sdk.JsonSerializationContexts;
using Vion.ServiceProvider.Sdk.RegistrationFlow;
using Vion.ServiceProvider.Sdk.SystemControl;
using Vion.ServiceProvider.Sdk.Test.TestHelpers;

namespace Vion.ServiceProvider.Sdk.Test.SystemControl
{
    [TestClass]
    public class SetLogLevelHandlerShould
    {
        private readonly Mock<ILogger<SetLogLevelHandler>> _loggerMock = new();

        private SetLogLevelHandler _sut = null!;

        [TestInitialize]
        public void Initialize()
        {
            _sut = new SetLogLevelHandler(_loggerMock.Object);
        }

        [DataRow(LogLevel.Trace, LogLevel.Warning)]
        [DataRow(LogLevel.Critical, LogLevel.Debug)]
        [TestMethod]
        public async Task ApplyLogLevel(LogLevel initial, LogLevel requested)
        {
            // Arrange
            LogLevelManager.CurrentLevel = initial;
            var payload = JsonSerializer.SerializeToUtf8Bytes(new SetLogLevelPayload(requested), ServiceProviderJsonContext.Default.SetLogLevelPayload);
            var message = MqttApplicationMessageBuilder.BuildJson(Guid.NewGuid().ToString(), payload, nameof(SetLogLevelPayload));

            // Act — the correlation ID is the one the boundary extracted and passes to every handler.
            await _sut.HandleAsync(Mock.Of<IServiceProviderPublish>(), message, Guid.NewGuid(), CancellationToken.None);

            // Assert
            Assert.AreEqual(requested, LogLevelManager.CurrentLevel);
        }
    }
}