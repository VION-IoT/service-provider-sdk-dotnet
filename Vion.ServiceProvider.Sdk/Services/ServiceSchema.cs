using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Vion.Contracts.Events.MeshToCloud;
using Vion.Contracts.TypeRef;

namespace Vion.ServiceProvider.Sdk.Services
{
    /// <summary>Identity and field catalog for a service exposed by this SP.</summary>
    /// <typeparam name="TService">The service type whose fields are catalogued.</typeparam>
    public abstract class ServiceSchema<TService>
    {
        private Dictionary<string, IServiceField<TService>>? _fieldByName;

        // ReSharper disable once MemberCanBeProtected.Global
        /// <summary>The identifier of the service.</summary>
        public abstract string ServiceIdentifier { get; }

        /// <summary>The description of the service.</summary>
        protected abstract string ServiceDescription { get; }

        // ReSharper disable once MemberCanBeProtected.Global
        /// <summary>The fields the service exposes.</summary>
        public abstract IReadOnlyList<IServiceField<TService>> All { get; }

        /// <summary>Looks up a field by its <see cref="IServiceField.Name" />.</summary>
        /// <param name="name">The field identifier to look up.</param>
        /// <param name="field">The matched field, or <c>null</c> if no field has the given name.</param>
        /// <returns><c>true</c> if a field was found; otherwise <c>false</c>.</returns>
        public bool TryGet(string name, [NotNullWhen(true)] out IServiceField<TService>? field)
        {
            _fieldByName ??= All.ToDictionary(f => f.Name);
            return _fieldByName.TryGetValue(name, out field);
        }

        /// <summary>Builds the SP-declaration entry for this schema.</summary>
        /// <returns>The declaration entry describing this service's properties and measuring points.</returns>
        public ServiceProviderDeclarationPayload.ServiceInfo BuildServiceInfo()
        {
            var properties = All.Where(field => field.Kind == ServiceFieldKind.Property).Select(BuildPropertyInfo).ToList();
            var measuringPoints = All.Where(field => field.Kind == ServiceFieldKind.MeasuringPoint).Select(BuildMeasuringPointInfo).ToList();
            return new ServiceProviderDeclarationPayload.ServiceInfo
                   {
                       Identifier = ServiceIdentifier,
                       Description = ServiceDescription,
                       Properties = properties.Count > 0 ? properties : null,
                       MeasuringPoints = measuringPoints.Count > 0 ? measuringPoints : null,
                   };
        }

        private static ServiceProviderDeclarationPayload.PropertyInfo BuildPropertyInfo(IServiceField field)
        {
            return new ServiceProviderDeclarationPayload.PropertyInfo
                   {
                       Identifier = field.Name,
                       Schema = field.Schema.ToJsonSchema(),
                   };
        }

        private static ServiceProviderDeclarationPayload.MeasuringPointInfo BuildMeasuringPointInfo(IServiceField field)
        {
            return new ServiceProviderDeclarationPayload.MeasuringPointInfo
                   {
                       Identifier = field.Name,
                       Schema = field.Schema.ToJsonSchema(),
                   };
        }
    }
}
