using System.Linq;
using System.Text.Json.Nodes;
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
        public void PreserveWriteOnlyInTheEmittedPropertySchema()
        {
            var service = new TestSchema().BuildServiceInfo();

            var secret = service.Properties!.Single(p => p.Identifier == "Secret");
            Assert.IsTrue(secret.Schema["writeOnly"]!.GetValue<bool>());
        }
    }
}
