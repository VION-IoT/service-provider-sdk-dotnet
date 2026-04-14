using System.Collections.Generic;

namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    public static class Payloads
    {
        public class ServiceProviderSetupSchemaPayload
        {
            public required List<SchemaField> Fields { get; init; }
        }

        public class SchemaField
        {
            public required string Identifier { get; init; }

            public required string Name { get; init; }

            public required string Group { get; init; }

            public required string Type { get; init; }

            public required object Default { get; init; }

            public FieldOptions? Options { get; init; }

            public VisibilityCondition? VisibleWhen { get; init; }
        }

        public class FieldOptions
        {
            public required List<object> Values { get; init; }
        }

        public class VisibilityCondition
        {
            public required string FieldId { get; init; }

            public required object Equals { get; init; }
        }

        public class ServiceProviderSetupSelectionPayload
        {
            public required Dictionary<string, object> Values { get; init; }
        }
    }
}