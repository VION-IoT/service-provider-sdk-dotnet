using System;
using System.Diagnostics;
using System.Text;
using MQTTnet;
using MQTTnet.Packets;
using Vion.Contracts.Mqtt;

namespace Vion.ServiceProvider.Sdk.Tracing
{
    public static class ActivityExtensions
    {
        extension(MqttApplicationMessageBuilder builder)
        {
            /// <summary>
            ///     Adds the current activity context to the MQTT application message for distributed tracing.
            /// </summary>
            /// <returns>The MQTT application message builder with the activity context added.</returns>
            public MqttApplicationMessageBuilder WithActivity()
            {
                // Add distributed tracing support
                if (Activity.Current?.Id != null)
                {
                    builder.WithUserProperty(MqttUserProperties.TraceParent.Name, Encoding.UTF8.GetBytes(Activity.Current.Id));
                }

                return builder;
            }
        }

        extension(MqttApplicationMessageReceivedEventArgs eventArgs)
        {
            /// <summary>
            ///     Gets the parent activity context from the MQTT application message.
            /// </summary>
            /// <returns>The parent activity context if available; otherwise, null.</returns>
            public ActivityContext GetParentContext()
            {
                var traceParent = eventArgs.ApplicationMessage.UserProperties?.Find(property => property.Name == MqttUserProperties.TraceParent.Name)?.ReadValueAsString();
                ActivityContext.TryParse(traceParent, null, true, out var parentContext);
                return parentContext;
            }

            /// <summary>
            ///     Starts a new activity with the specified name and kind, using the parent context from the MQTT application message.
            /// </summary>
            /// <param name="name">The name of the activity.</param>
            /// <param name="kind">The kind of the activity.</param>
            /// <returns>The started activity if successful; otherwise, null.</returns>
            public Activity? StartActivity(string name, ActivityKind kind)
            {
                var parentContext = eventArgs.GetParentContext();
                var activity = ActivitySources.Messaging.StartActivity(name, kind, parentContext);
                activity?.SetMqttTopic(eventArgs.ApplicationMessage.Topic);
                return activity;
            }
        }

        extension(Activity activity)
        {
            /// <summary>
            ///     Sets the MQTT topic for the current activity.
            /// </summary>
            /// <param name="topic">The MQTT topic.</param>
            public void SetMqttTopic(string topic)
            {
                activity.SetTag("mqtt.topic", topic);
            }

            /// <summary>
            ///     Marks the current activity as failed due to an exception.
            /// </summary>
            /// <param name="exception">The exception that caused the failure.</param>
            public void MarkFailed(Exception exception)
            {
                activity.SetStatus(ActivityStatusCode.Error, exception.Message);
                activity.SetTag("exception.type", exception.GetType().Name);
            }

            /// <summary>
            ///     Marks the current activity as failed with a custom description.
            /// </summary>
            /// <param name="description">The description of the failure.</param>
            public void MarkFailed(string description)
            {
                activity.SetStatus(ActivityStatusCode.Error, description);
            }

            /// <summary>
            ///     Marks the current activity and all its parent activities as explicitly trace-enabled.
            /// </summary>
            /// <remarks>
            ///     By default, tracing is enabled for all activities, but the collector may filter out certain spans,
            ///     such as those where <c>mqtt.topic</c> matches <c>.*property/state.*</c>.
            ///     Invoking this method sets the <c>trace.enabled</c> tag to <c>true</c> on the activity and its
            ///     parent chain, ensuring these spans are not excluded by such filters.
            /// </remarks>
            public void EnableTracing()
            {
                activity.SetTag("trace.enabled", true);
                var parentActivity = activity.Parent;
                while (parentActivity != null)
                {
                    parentActivity.SetTag("trace.enabled", true);
                    parentActivity = parentActivity.Parent;
                }
            }

            /// <summary>
            ///     Sets the installation topic for the current activity.
            /// </summary>
            /// <param name="installationTopic">The installation topic.</param>
            public void SetInstallationTopic(string installationTopic)
            {
                activity.SetTag("installation.topic", installationTopic);
            }

            /// <summary>
            ///     Sets the service ID for the current activity.
            /// </summary>
            /// <param name="id">The service ID.</param>
            public void SetServiceId(string id)
            {
                activity.SetTag("service.id", id);
            }

            /// <summary>
            ///     Sets the property identifier for the current activity.
            /// </summary>
            /// <param name="identifier">The property identifier.</param>
            public void SetPropertyIdentifier(string identifier)
            {
                activity.SetTag("property.identifier", identifier);
            }

            /// <summary>
            ///     Sets the measuring point identifier for the current activity.
            /// </summary>
            /// <param name="identifier">The measuring point identifier.</param>
            public void SetMeasuringPointIdentifier(string identifier)
            {
                activity.SetTag("measuringPoint.identifier", identifier);
            }
        }
    }
}