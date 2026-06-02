using System.Collections.Generic;
using System.Text.Json.Nodes;
using Vion.Contracts.TypeRef;

namespace Vion.ServiceProvider.Sdk.Services
{
    /// <summary>A single field a service exposes — a property or measuring point — described by its wire-format schema.</summary>
    public interface IServiceField
    {
        /// <summary>The identifier used on the MQTT topic and in the SP declaration.</summary>
        string Name { get; }

        /// <summary>Whether the field is exposed as a property or a measuring point.</summary>
        ServiceFieldKind Kind { get; }

        /// <summary>The single source of truth for the field's wire-format type and its JSON-schema annotations.</summary>
        TypeSchema Schema { get; }

        /// <summary>The JSON schema literal embedded in the SP declaration. Derived from <see cref="Schema" />.</summary>
        string JsonSchema { get; }

        /// <summary>Whether the field accepts incoming <c>property/set</c> updates. Derived from <see cref="Schema" />.</summary>
        bool IsWritable { get; }

        /// <summary>Whether the field's broadcast value should be redacted. Derived from <see cref="Schema" />.</summary>
        bool IsWriteOnly { get; }

        /// <summary>Free-form annotations included in the SP declaration.</summary>
        IReadOnlyDictionary<string, object>? Annotations { get; }
    }

    /// <typeparam name="TService">The service type this field belongs to.</typeparam>
    public interface IServiceField<TService> : IServiceField
    {
        /// <summary>Reads the field's value from a service snapshot.</summary>
        /// <param name="service">The snapshot to read from.</param>
        /// <returns>The field's value as JSON, or <c>null</c> if unset.</returns>
        JsonNode? ReadFrom(TService service);

        /// <summary>Returns a new snapshot with the field updated.</summary>
        /// <param name="service">The snapshot to derive from.</param>
        /// <param name="raw">The new value, or <c>null</c> to clear the field.</param>
        TService WriteTo(TService service, JsonNode? raw);
    }
}