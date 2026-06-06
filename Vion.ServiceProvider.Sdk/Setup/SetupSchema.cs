using System.Collections.Generic;

namespace Vion.ServiceProvider.Sdk.Setup
{
    /// <summary>
    ///     Represents the setup schema payload containing field definitions presented to the operator before declaration.
    /// </summary>
    public class ServiceProviderSetupSchemaPayload
    {
        /// <summary>
        ///     Gets or initializes the list of schema fields.
        /// </summary>
        public required List<SchemaField> Fields { get; init; }
    }

    /// <summary>
    ///     Represents a single field in the setup schema.
    /// </summary>
    public class SchemaField
    {
        /// <summary>
        ///     Gets or initializes the unique identifier for the field.
        /// </summary>
        public required string Identifier { get; init; }

        /// <summary>
        ///     Gets or initializes the display name of the field.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        ///     Gets or initializes the group to which this field belongs.
        /// </summary>
        public required string Group { get; init; }

        /// <summary>
        ///     Gets or initializes the data type of the field.
        /// </summary>
        public required string Type { get; init; }

        /// <summary>
        ///     Gets or initializes the default value for the field.
        /// </summary>
        public required object Default { get; init; }

        /// <summary>
        ///     Gets or initializes the optional field options (e.g., for dropdown values).
        /// </summary>
        public FieldOptions? Options { get; init; }

        /// <summary>
        ///     Gets or initializes the optional visibility condition for conditional field display.
        /// </summary>
        public VisibilityCondition? VisibleWhen { get; init; }
    }

    /// <summary>
    ///     Represents the options available for a field (e.g., dropdown values).
    /// </summary>
    public class FieldOptions
    {
        /// <summary>
        ///     Gets or initializes the list of available values for the field.
        /// </summary>
        public required List<object> Values { get; init; }
    }

    /// <summary>
    ///     Represents a condition for field visibility based on another field's value.
    /// </summary>
    public class VisibilityCondition
    {
        /// <summary>
        ///     Gets or initializes the identifier of the field to check.
        /// </summary>
        public required string FieldId { get; init; }

        /// <summary>
        ///     Gets or initializes the value the referenced field must equal for this field to be visible.
        /// </summary>
        public new required object Equals { get; init; }
    }

    /// <summary>
    ///     Represents the operator's selection values for the setup schema.
    /// </summary>
    public class ServiceProviderSetupSelectionPayload
    {
        /// <summary>
        ///     Gets or initializes the dictionary of field identifiers to their selected values.
        /// </summary>
        public required Dictionary<string, object> Values { get; init; }
    }
}
