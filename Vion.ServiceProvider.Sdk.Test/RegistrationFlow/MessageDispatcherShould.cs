using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using MQTTnet;
using MQTTnet.Packets;
using Vion.ServiceProvider.Sdk.RegistrationFlow;

namespace Vion.ServiceProvider.Sdk.Test.RegistrationFlow
{
    [TestClass]
    public class MessageDispatcherShould
    {
        private readonly Mock<IServiceProviderClientHandler> _clientMock = new();

        private readonly Mock<ILogger> _loggerMock = new();

        private int _fallbackInvocationCount;

        private int _handlerInvocationCount;

        private MessageDispatcher _sut = null!;

        [TestInitialize]
        public void Initialize()
        {
            _sut = new MessageDispatcher(_loggerMock.Object);
        }

        [DataRow("foo/bar", "foo/bar", DisplayName = "literal")]
        [DataRow("some/+/topic", "some/abc/topic", DisplayName = "single-level wildcard")]
        [DataRow("some/#", "some/x/y/z", DisplayName = "multi-level wildcard")]
        [DataRow("a/+/b/#", "a/1/b/2/3", DisplayName = "mixed wildcards")]
        [TestMethod]
        public async Task InvokeHandlerWhenTopicMatchesFilter(string topicFilter, string messageTopic)
        {
            // Arrange
            var handlers = new[] { CreateHandlerConfiguration(topicFilter) };

            // Act
            await _sut.DispatchAsync(CreateMessageArgs(messageTopic), _clientMock.Object, handlers, null);

            // Assert
            Assert.AreEqual(1, _handlerInvocationCount);
        }

        [DataRow("some/+/topic", "other/abc/topic", DisplayName = "different first segment")]
        [DataRow("some/+/topic", "some/abc/other", DisplayName = "different last segment")]
        [DataRow("some/+/topic", "some/abc", DisplayName = "fewer segments than filter")]
        [DataRow("some/+/topic", "some/a/b/topic", DisplayName = "more segments than filter")]
        [TestMethod]
        public async Task NotInvokeHandlerWhenTopicDoesNotMatchFilter(string topicFilter, string messageTopic)
        {
            // Arrange
            var handlers = new[] { CreateHandlerConfiguration(topicFilter) };

            // Act
            await _sut.DispatchAsync(CreateMessageArgs(messageTopic), _clientMock.Object, handlers, null);

            // Assert
            Assert.AreEqual(0, _handlerInvocationCount);
        }

        [TestMethod]
        public async Task InvokeFallbackWhenNoHandlerMatches()
        {
            // Arrange
            var handlers = new[] { CreateHandlerConfiguration("some/very/specific/topic") };

            // Act
            await _sut.DispatchAsync(CreateMessageArgs("some/other/topic"), _clientMock.Object, handlers, RecordFallbackInvocationAsync);

            // Assert
            Assert.AreEqual(1, _fallbackInvocationCount);
        }

        [TestMethod]
        public async Task NotInvokeFallbackWhenHandlerMatches()
        {
            // Arrange
            var handlers = new[] { CreateHandlerConfiguration("foo/bar") };

            // Act
            await _sut.DispatchAsync(CreateMessageArgs("foo/bar"), _clientMock.Object, handlers, RecordFallbackInvocationAsync);

            // Assert
            Assert.AreEqual(0, _fallbackInvocationCount);
        }

        [TestMethod]
        public async Task InvokeAllHandlersWhenMultipleMatch()
        {
            // Arrange
            var handlers = new[]
                           {
                               CreateHandlerConfiguration("foo/bar"),
                               CreateHandlerConfiguration("foo/+"),
                           };

            // Act
            await _sut.DispatchAsync(CreateMessageArgs("foo/bar"), _clientMock.Object, handlers, null);

            // Assert
            Assert.AreEqual(2, _handlerInvocationCount);
        }

        [TestMethod]
        public async Task InvokeRemainingHandlersWhenOneThrows()
        {
            // Arrange
            var handlers = new[]
                           {
                               CreateHandlerConfiguration("foo/bar", true),
                               CreateHandlerConfiguration("foo/+"),
                           };

            // Act
            await _sut.DispatchAsync(CreateMessageArgs("foo/bar"), _clientMock.Object, handlers, null);

            // Assert
            Assert.AreEqual(2, _handlerInvocationCount);
        }

        private HandlerConfiguration CreateHandlerConfiguration(string topicFilter, bool shouldThrow = false)
        {
            return new HandlerConfiguration(topicFilter, (_, _) => RecordHandlerInvocationAsync(shouldThrow), false, topicFilter);
        }

        private Task RecordHandlerInvocationAsync(bool shouldThrow)
        {
            _handlerInvocationCount++;
            return shouldThrow ? throw new InvalidOperationException() : Task.CompletedTask;
        }

        private Task RecordFallbackInvocationAsync(MqttApplicationMessageReceivedEventArgs args)
        {
            _fallbackInvocationCount++;
            return Task.CompletedTask;
        }

        private static MqttApplicationMessageReceivedEventArgs CreateMessageArgs(string topic)
        {
            var message = new MqttApplicationMessageBuilder().WithTopic(topic).Build();
            var publishPacket = new MqttPublishPacket { Topic = topic };
            return new MqttApplicationMessageReceivedEventArgs(Guid.NewGuid().ToString(), message, publishPacket, (_, _) => Task.CompletedTask);
        }
    }
}