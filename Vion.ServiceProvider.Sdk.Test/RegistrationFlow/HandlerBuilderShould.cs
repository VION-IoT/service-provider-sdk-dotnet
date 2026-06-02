using System;
using System.Threading.Tasks;
using Vion.Contracts.Events.MeshToCloud;
using Vion.ServiceProvider.Sdk.RegistrationFlow;

namespace Vion.ServiceProvider.Sdk.Test.RegistrationFlow
{
    [TestClass]
    public class HandlerBuilderShould
    {
        private static readonly string InstallationTopic = Guid.NewGuid().ToString();

        private static readonly string ServiceProviderIdentifier = Guid.NewGuid().ToString();

        private static readonly ServiceProviderDeclarationPayload Declaration = new() { Services = [] };

        private static readonly ServiceProviderMessageHandler NoopHandler = (_, _, _, _) => Task.CompletedTask;

        private readonly HandlerBuilder _sut = new(InstallationTopic, ServiceProviderIdentifier, Declaration);

        [DataRow("foo/bar")]
        [DataRow("singleSegment")]
        [DataRow("foo/+/bar")]
        [DataRow("foo/#")]
        [DataRow("a/+/b/+/c")]
        [DataRow("a/+/b/#")]
        [TestMethod]
        public void AddHandlerForGivenTopic(string topic)
        {
            // Arrange

            // Act
            _sut.WithHandler(topic, NoopHandler);

            // Assert
            Assert.HasCount(1, _sut.ConfigHandlers);
            Assert.AreEqual(topic, _sut.ConfigHandlers[0].TopicPartToMatch);
            Assert.AreSame(NoopHandler, _sut.ConfigHandlers[0].Handler);
        }
    }
}