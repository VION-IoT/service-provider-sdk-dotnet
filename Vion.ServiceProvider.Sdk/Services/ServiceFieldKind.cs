namespace Vion.ServiceProvider.Sdk.Services
{
    /// <summary>Whether a service field is exposed to mesh as a property or a measuring point.</summary>
    public enum ServiceFieldKind
    {
        /// <summary>A property — readable, and writable unless its schema is read-only.</summary>
        Property,

        /// <summary>A measuring point — telemetry by the service provider.</summary>
        MeasuringPoint,
    }
}