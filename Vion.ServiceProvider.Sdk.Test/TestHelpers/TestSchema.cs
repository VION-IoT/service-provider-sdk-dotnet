using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Vion.Contracts.TypeRef;
using Vion.ServiceProvider.Sdk.Services;

namespace Vion.ServiceProvider.Sdk.Test.TestHelpers
{
    // Public (not internal) so Moq's proxy generator can reference it where it surfaces as a generic argument of a
    // mocked collaborator (e.g. ILogger<ServiceStateStore<TestServiceState>>) without granting InternalsVisibleTo to the proxy assembly.
    public sealed record TestServiceState(string Plain = "", string? Secret = null, double Reading = 0);

    [JsonSourceGenerationOptions(WriteIndented = true,
                                 DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                                 PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
                                 DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(TestServiceState))]
    internal sealed partial class TestServiceStateJsonContext : JsonSerializerContext;

    internal sealed class TestSchema : ServiceSchema<TestServiceState>
    {
        public static readonly IServiceField<TestServiceState> Plain = new ServiceField<TestServiceState>("Plain",
                                                                                                          ServiceFieldKind.Property,
                                                                                                          TypeSchema.Of(new PrimitiveTypeRef(PrimitiveKind.String)),
                                                                                                          state => JsonValue.Create(state.Plain),
                                                                                                          (state, value) => state with { Plain = value?.GetValue<string>() ?? "" });

        public static readonly IServiceField<TestServiceState> Secret = new ServiceField<TestServiceState>("Secret",
                                                                                                           ServiceFieldKind.Property,
                                                                                                           new TypeSchema(new NullableTypeRef(new PrimitiveTypeRef(PrimitiveKind
                                                                                                                   .String)),
                                                                                                               new TypeAnnotations { WriteOnly = true },
                                                                                                               ImmutableDictionary<string, TypeAnnotations>.Empty),
                                                                                                           state => state.Secret is null ? null : JsonValue.Create(state.Secret),
                                                                                                           (state, value) => state with { Secret = value?.GetValue<string>() });

        public static readonly IServiceField<TestServiceState> Reading = new ServiceField<TestServiceState>("Reading",
                                                                                                            ServiceFieldKind.MeasuringPoint,
                                                                                                            TypeSchema.Of(new PrimitiveTypeRef(PrimitiveKind.Double)),
                                                                                                            state => JsonValue.Create(state.Reading),
                                                                                                            (state, value) =>
                                                                                                                state with { Reading = value?.GetValue<double>() ?? 0 });

        public override string ServiceIdentifier
        {
            get => "test-service";
        }

        protected override string ServiceDescription
        {
            get => "Test service";
        }

        public override IReadOnlyList<IServiceField<TestServiceState>> All
        {
            get => [Plain, Secret, Reading];
        }
    }
}