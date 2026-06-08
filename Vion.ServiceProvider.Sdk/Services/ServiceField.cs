using System;
using System.Text.Json.Nodes;
using Vion.Contracts.TypeRef;

namespace Vion.ServiceProvider.Sdk.Services
{
    /// <summary>
    ///     A field exposed by a service, backed by read/write delegates over the service's state snapshot. The wire-format
    ///     type and its JSON-schema annotations both derive from a single <see cref="TypeSchema" />.
    /// </summary>
    /// <typeparam name="TService">The service type this field belongs to.</typeparam>
    public sealed class ServiceField<TService> : IServiceField<TService>
    {
        private readonly Func<TService, JsonNode?> _read;

        private readonly Func<TService, JsonNode?, TService> _write;

        /// <summary>Initializes a new instance of the <see cref="ServiceField{TService}" /> class.</summary>
        /// <param name="name">The identifier used on the MQTT topic and in the SP declaration.</param>
        /// <param name="kind">Whether the field is a property or a measuring point.</param>
        /// <param name="schema">The single source of truth for the field's wire-format type and annotations.</param>
        /// <param name="read">Reads the field's value from a service snapshot.</param>
        /// <param name="write">Returns a new snapshot with the field updated.</param>
        /// <param name="presentation">Optional UI presentation hints, emitted as the declaration's <c>presentation</c> sibling.</param>
        public ServiceField(string name,
                            ServiceFieldKind kind,
                            TypeSchema schema,
                            Func<TService, JsonNode?> read,
                            Func<TService, JsonNode?, TService> write,
                            Presentation? presentation = null)
        {
            Name = name;
            Kind = kind;
            Schema = schema;
            JsonSchema = schema.ToJsonSchema().ToJsonString();
            Presentation = presentation;
            _read = read;
            _write = write;
        }

        /// <inheritdoc />
        public string Name { get; }

        /// <inheritdoc />
        public ServiceFieldKind Kind { get; }

        /// <inheritdoc />
        public TypeSchema Schema { get; }

        /// <inheritdoc />
        public string JsonSchema { get; }

        /// <inheritdoc />
        public bool IsWritable
        {
            get => !Schema.Annotations.ReadOnly;
        }

        /// <inheritdoc />
        public bool IsWriteOnly
        {
            get => Schema.Annotations.WriteOnly;
        }

        /// <inheritdoc />
        public Presentation? Presentation { get; }

        /// <inheritdoc />
        public JsonNode? ReadFrom(TService service)
        {
            return _read(service);
        }

        /// <inheritdoc />
        public TService WriteTo(TService service, JsonNode? raw)
        {
            return _write(service, raw);
        }
    }
}
