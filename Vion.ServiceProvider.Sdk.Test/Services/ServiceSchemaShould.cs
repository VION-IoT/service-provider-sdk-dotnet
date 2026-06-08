using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;
using Vion.Contracts.TypeRef;
using Vion.ServiceProvider.Sdk.Services;
using Vion.ServiceProvider.Sdk.Test.TestHelpers;

namespace Vion.ServiceProvider.Sdk.Test.Services
{
    [TestClass]
    public class ServiceSchemaShould
    {
        [TestMethod]
        public void EmitAStructuredSchemaForEachPropertyAndMeasuringPoint()
        {
            var service = new TestSchema().BuildServiceInfo();

            var plain = service.Properties!.Single(p => p.Identifier == "Plain");
            Assert.IsTrue(plain.Schema is JsonObject, "schema must be the structured document, not a stringified type token");
            Assert.AreEqual("string", plain.Schema["type"]!.GetValue<string>());
            Assert.IsNull(plain.Presentation);
            Assert.IsNull(plain.Runtime);

            var reading = service.MeasuringPoints!.Single(m => m.Identifier == "Reading");
            Assert.AreEqual("number", reading.Schema["type"]!.GetValue<string>());
            Assert.AreEqual("double", reading.Schema["format"]!.GetValue<string>());
        }

        [TestMethod]
        public void CarryUnitReadOnlyAndWriteOnlyInsideTheEmittedSchema()
        {
            var service = new AnnotatedSchema().BuildServiceInfo();

            var voltage = service.Properties!.Single(p => p.Identifier == "Voltage");
            Assert.AreEqual("V", voltage.Schema["x-unit"]!.GetValue<string>());

            var serial = service.Properties!.Single(p => p.Identifier == "Serial");
            Assert.IsTrue(serial.Schema["readOnly"]!.GetValue<bool>());

            var secret = service.Properties!.Single(p => p.Identifier == "Secret");
            Assert.IsTrue(secret.Schema["writeOnly"]!.GetValue<bool>());
        }

        [TestMethod]
        public void EmitPresentationAsASiblingWhenAFieldDeclaresIt()
        {
            var service = new AnnotatedSchema().BuildServiceInfo();

            var grouped = service.Properties!.Single(p => p.Identifier == "Grouped");
            Assert.AreEqual("Connection", grouped.Presentation!["group"]!.GetValue<string>());
            Assert.AreEqual(2, grouped.Presentation["order"]!.GetValue<int>());
            Assert.AreEqual("string", grouped.Schema["type"]!.GetValue<string>());
        }

        private sealed class AnnotatedSchema : ServiceSchema<object>
        {
            public override string ServiceIdentifier
            {
                get => "annotated";
            }

            protected override string ServiceDescription
            {
                get => "annotated";
            }

            public override IReadOnlyList<IServiceField<object>> All
            {
                get =>
                [
                    Field("Voltage",
                          new TypeSchema(new PrimitiveTypeRef(PrimitiveKind.Double), new TypeAnnotations { Unit = "V" }, ImmutableDictionary<string, TypeAnnotations>.Empty)),
                    Field("Serial",
                          new TypeSchema(new PrimitiveTypeRef(PrimitiveKind.String), new TypeAnnotations { ReadOnly = true }, ImmutableDictionary<string, TypeAnnotations>.Empty)),
                    Field("Secret",
                          new TypeSchema(new PrimitiveTypeRef(PrimitiveKind.String), new TypeAnnotations { WriteOnly = true }, ImmutableDictionary<string, TypeAnnotations>.Empty)),
                    Field("Grouped", TypeSchema.Of(new PrimitiveTypeRef(PrimitiveKind.String)), new Presentation { Group = "Connection", Order = 2 }),
                ];
            }

            private static IServiceField<object> Field(string name, TypeSchema schema, Presentation? presentation = null)
            {
                return new ServiceField<object>(name,
                                                ServiceFieldKind.Property,
                                                schema,
                                                _ => null,
                                                (state, _) => state,
                                                presentation);
            }
        }
    }
}
