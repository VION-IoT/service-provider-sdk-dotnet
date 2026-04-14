using System.Threading;
using System.Threading.Tasks;
using MQTTnet;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    public interface IServiceProviderPublish
    {
        Task PublishAsync(string topic, string schema, string contentType, byte[] payload, CancellationToken cancellationToken);

        Task PublishAsync(MqttApplicationMessage msg, CancellationToken cancellationToken);
    }
}