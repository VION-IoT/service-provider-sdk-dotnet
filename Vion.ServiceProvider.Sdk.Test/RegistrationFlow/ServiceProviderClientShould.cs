using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using MQTTnet;
using MQTTnet.Packets;
using Vion.Contracts.Mqtt;
using Vion.ServiceProvider.Sdk.RegistrationFlow;
using Vion.Telemetry.Instrumentation;
using MqttApplicationMessageBuilder = Vion.ServiceProvider.Sdk.Test.TestHelpers.MqttApplicationMessageBuilder;

namespace Vion.ServiceProvider.Sdk.Test.RegistrationFlow
{
    // The client is never started, so a publish fails on the unconnected MQTT client and a received message finds no handler. Both still pass the span site, which is all these
    // tests observe. A span is recorded only while a listener samples the messaging source, so each test registers one, and the listener keeps only the spans on the test's own
    // topic.
    [TestClass]
    public class ServiceProviderClientShould
    {
        private readonly ServiceProviderClient _sut = new(new ServiceProviderClientConfiguration
                                                          {
                                                              ConnectionData = new MqttConnectionData(Guid.NewGuid().ToString(),
                                                                                                      Guid.NewGuid().ToString(),
                                                                                                      1883),
                                                              Secret = Guid.NewGuid().ToString(),
                                                              RegistrationCredentials = RegistrationCredentials.WellKnown,
                                                              OperationalMqttDataStore = new Mock<IOperationalMqttDataStore>().Object,
                                                          },
                                                          new MqttClientFactory(),
                                                          new Mock<ILogger>().Object);

        [DataRow(Topics.PropertyState)]
        [DataRow(Topics.MeasuringPointState)]
        [DataRow(Topics.ComponentHealth)]
        [DataRow(Topics.DiState)]
        [DataRow(Topics.DoState)]
        [DataRow(Topics.AiState)]
        [DataRow(Topics.AoState)]
        [DataRow(Topics.ModbusGet)]
        [TestMethod]
        public async Task LeavePublishUntracedWhenTopicStateOrPoll(string topicSuffix)
        {
            // Arrange
            var topic = $"{Guid.NewGuid()}/{Guid.NewGuid()}{topicSuffix}";
            var spans = new List<Activity>();
            using var listener = ListenForSpans(topic, spans);

            // Act
            await _sut.PublishMessageAsync(topic, Guid.NewGuid(), CancellationToken.None);

            // Assert
            Assert.IsEmpty(spans);
        }

        [DataRow(Topics.DoSet)]
        [DataRow(Topics.AoSet)]
        [DataRow(Topics.ModbusSet)]
        [TestMethod]
        public async Task TracePublishWhenTopicCommand(string topicSuffix)
        {
            // Arrange
            var topic = $"{Guid.NewGuid()}/{Guid.NewGuid()}{topicSuffix}";
            var spans = new List<Activity>();
            using var listener = ListenForSpans(topic, spans);

            // Act
            await _sut.PublishMessageAsync(topic, Guid.NewGuid(), CancellationToken.None);

            // Assert
            Assert.HasCount(1, spans);
            Assert.AreEqual(MessagingSpanNames.Publish, spans[0].DisplayName);
        }

        [DataRow(Topics.PropertyState)]
        [DataRow(Topics.MeasuringPointState)]
        [DataRow(Topics.ComponentHealth)]
        [DataRow(Topics.DiState)]
        [DataRow(Topics.DoState)]
        [DataRow(Topics.AiState)]
        [DataRow(Topics.AoState)]
        [DataRow(Topics.ModbusGet)]
        [TestMethod]
        public async Task LeaveConsumeUntracedWhenTopicStateOrPoll(string topicSuffix)
        {
            // Arrange
            var topic = $"{Guid.NewGuid()}/{Guid.NewGuid()}{topicSuffix}";
            var spans = new List<Activity>();
            using var listener = ListenForSpans(topic, spans);
            var received = new MqttApplicationMessageReceivedEventArgs(Guid.NewGuid().ToString(),
                                                                       MqttApplicationMessageBuilder.BuildEmptyPayload(topic),
                                                                       new MqttPublishPacket(),
                                                                       (_, _) => Task.CompletedTask);

            // Act
            await _sut.OnApplicationMessageReceivedAsync(received);

            // Assert
            Assert.IsEmpty(spans);
        }

        [DataRow(Topics.DoSet)]
        [DataRow(Topics.AoSet)]
        [DataRow(Topics.ModbusSet)]
        [TestMethod]
        public async Task TraceConsumeWhenTopicCommand(string topicSuffix)
        {
            // Arrange
            var topic = $"{Guid.NewGuid()}/{Guid.NewGuid()}{topicSuffix}";
            var spans = new List<Activity>();
            using var listener = ListenForSpans(topic, spans);
            var received = new MqttApplicationMessageReceivedEventArgs(Guid.NewGuid().ToString(),
                                                                       MqttApplicationMessageBuilder.BuildEmptyPayload(topic),
                                                                       new MqttPublishPacket(),
                                                                       (_, _) => Task.CompletedTask);

            // Act
            await _sut.OnApplicationMessageReceivedAsync(received);

            // Assert
            Assert.HasCount(1, spans);
            Assert.AreEqual(MessagingSpanNames.Consume, spans[0].DisplayName);
        }

        private static ActivityListener ListenForSpans(string topic, List<Activity> spans)
        {
            var listener = new ActivityListener
                           {
                               ShouldListenTo = source => source.Name == ActivitySources.MessagingSourceName,
                               Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
                               ActivityStopped = activity =>
                                                 {
                                                     if (Equals(activity.GetTagItem("messaging.destination.name"), topic))
                                                     {
                                                         spans.Add(activity);
                                                     }
                                                 },
                           };
            ActivitySource.AddActivityListener(listener);
            return listener;
        }
    }
}
