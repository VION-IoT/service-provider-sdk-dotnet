using System;
using System.Collections.Immutable;
using Vion.Contracts.TypeRef;
using Vion.ServiceProvider.Sdk.Services;

namespace Vion.ServiceProvider.Sdk.Test.Services
{
    [TestClass]
    public class ServiceFieldShould
    {
        private static readonly TypeSchema Schema = TypeSchema.Of(new PrimitiveTypeRef(PrimitiveKind.String));

        [TestMethod]
        public void InvokeReadDelegate()
        {
            // Arrange
            var invoked = false;
            var sut = new ServiceField<TestService>(Guid.NewGuid().ToString(),
                                                    ServiceFieldKind.Property,
                                                    Schema,
                                                    _ =>
                                                    {
                                                        invoked = true;
                                                        return null;
                                                    },
                                                    (service, _) => service);

            // Act
            sut.ReadFrom(new TestService());

            // Assert
            Assert.IsTrue(invoked);
        }

        [TestMethod]
        public void InvokeWriteDelegate()
        {
            // Arrange
            var invoked = false;
            var sut = new ServiceField<TestService>(Guid.NewGuid().ToString(),
                                                    ServiceFieldKind.Property,
                                                    Schema,
                                                    _ => null,
                                                    (service, _) =>
                                                    {
                                                        invoked = true;
                                                        return service;
                                                    });

            // Act
            sut.WriteTo(new TestService(), null);

            // Assert
            Assert.IsTrue(invoked);
        }

        [TestMethod]
        public void DeriveJsonSchemaFromTypeSchema()
        {
            // Arrange

            // Act
            var sut = new ServiceField<TestService>(Guid.NewGuid().ToString(),
                                                    ServiceFieldKind.Property,
                                                    TypeSchema.Of(new PrimitiveTypeRef(PrimitiveKind.Bool)),
                                                    _ => null,
                                                    (service, _) => service);

            // Assert
            Assert.AreEqual("{\"type\":\"boolean\"}", sut.JsonSchema);
        }

        [TestMethod]
        public void ReportWriteOnlyFromTypeSchemaAnnotations()
        {
            // Arrange
            var schema = new TypeSchema(new PrimitiveTypeRef(PrimitiveKind.String), new TypeAnnotations { WriteOnly = true }, ImmutableDictionary<string, TypeAnnotations>.Empty);

            // Act
            var sut = new ServiceField<TestService>(Guid.NewGuid().ToString(), ServiceFieldKind.Property, schema, _ => null, (service, _) => service);

            // Assert
            Assert.IsTrue(sut.IsWriteOnly);
        }

        [TestMethod]
        public void NotReportWriteOnlyWhenNotAnnotated()
        {
            // Arrange

            // Act
            var sut = new ServiceField<TestService>(Guid.NewGuid().ToString(), ServiceFieldKind.Property, Schema, _ => null, (service, _) => service);

            // Assert
            Assert.IsFalse(sut.IsWriteOnly);
        }

        [TestMethod]
        public void ReportNotWritableWhenReadOnly()
        {
            // Arrange
            var schema = new TypeSchema(new PrimitiveTypeRef(PrimitiveKind.String), new TypeAnnotations { ReadOnly = true }, ImmutableDictionary<string, TypeAnnotations>.Empty);

            // Act
            var sut = new ServiceField<TestService>(Guid.NewGuid().ToString(), ServiceFieldKind.Property, schema, _ => null, (service, _) => service);

            // Assert
            Assert.IsFalse(sut.IsWritable);
        }

        [TestMethod]
        public void ReportWritableWhenNotReadOnly()
        {
            // Arrange

            // Act
            var sut = new ServiceField<TestService>(Guid.NewGuid().ToString(), ServiceFieldKind.Property, Schema, _ => null, (service, _) => service);

            // Assert
            Assert.IsTrue(sut.IsWritable);
        }

        private sealed record TestService;
    }
}