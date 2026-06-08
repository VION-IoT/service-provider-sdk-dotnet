using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using MQTTnet;
using Vion.ServiceProvider.Sdk.RegistrationFlow;

namespace Vion.ServiceProvider.Sdk.Test.RegistrationFlow
{
    [TestClass]
    public class MessageDispatcherShould
    {
        private readonly Mock<ILogger> _loggerMock = new();

        private readonly Mock<IServiceProviderPublisher> _publisherMock = new();

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
            await _sut.DispatchAsync(CreateMessage(messageTopic), _publisherMock.Object, handlers, Guid.NewGuid(), CancellationToken.None);

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
            await _sut.DispatchAsync(CreateMessage(messageTopic), _publisherMock.Object, handlers, Guid.NewGuid(), CancellationToken.None);

            // Assert
            Assert.AreEqual(0, _handlerInvocationCount);
        }

        [TestMethod]
        public async Task ReturnFalseWhenNoHandlerMatches()
        {
            // Arrange
            var handlers = new[] { CreateHandlerConfiguration("some/very/specific/topic") };

            // Act
            var handled = await _sut.DispatchAsync(CreateMessage("some/other/topic"), _publisherMock.Object, handlers, Guid.NewGuid(), CancellationToken.None);

            // Assert
            Assert.IsFalse(handled);
        }

        [TestMethod]
        public async Task ReturnTrueWhenHandlerMatches()
        {
            // Arrange
            var handlers = new[] { CreateHandlerConfiguration("foo/bar") };

            // Act
            var handled = await _sut.DispatchAsync(CreateMessage("foo/bar"), _publisherMock.Object, handlers, Guid.NewGuid(), CancellationToken.None);

            // Assert
            Assert.IsTrue(handled);
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
            await _sut.DispatchAsync(CreateMessage("foo/bar"), _publisherMock.Object, handlers, Guid.NewGuid(), CancellationToken.None);

            // Assert
            Assert.AreEqual(2, _handlerInvocationCount);
        }

        [TestMethod]
        public async Task InvokeRemainingHandlersWhenOneThrowsThenPropagateException()
        {
            // Arrange
            var handlers = new[]
                           {
                               CreateHandlerConfiguration("foo/bar", true),
                               CreateHandlerConfiguration("foo/+"),
                           };

            // Act / Assert — the throwing handler does not stop the others, and its exception propagates to the caller.
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _sut.DispatchAsync(CreateMessage("foo/bar"),
                                                                                                _publisherMock.Object,
                                                                                                handlers,
                                                                                                Guid.NewGuid(),
                                                                                                CancellationToken.None));
            Assert.AreEqual(2, _handlerInvocationCount);
        }

        [TestMethod]
        public async Task ThrowAggregateExceptionWhenMultipleHandlersThrow()
        {
            // Arrange
            var handlers = new[]
                           {
                               CreateHandlerConfiguration("foo/bar", true),
                               CreateHandlerConfiguration("foo/+", true),
                           };

            // Act / Assert
            await Assert.ThrowsExactlyAsync<AggregateException>(() => _sut.DispatchAsync(CreateMessage("foo/bar"),
                                                                                         _publisherMock.Object,
                                                                                         handlers,
                                                                                         Guid.NewGuid(),
                                                                                         CancellationToken.None));
            Assert.AreEqual(2, _handlerInvocationCount);
        }

        private HandlerConfiguration CreateHandlerConfiguration(string topicFilter, bool shouldThrow = false)
        {
            return new HandlerConfiguration(topicFilter, (_, _, _, _) => RecordHandlerInvocationAsync(shouldThrow), false, topicFilter);
        }

        private Task RecordHandlerInvocationAsync(bool shouldThrow)
        {
            _handlerInvocationCount++;
            return shouldThrow ? throw new InvalidOperationException() : Task.CompletedTask;
        }

        private static MqttApplicationMessage CreateMessage(string topic)
        {
            return new MqttApplicationMessageBuilder().WithTopic(topic).Build();
        }
    }
}
