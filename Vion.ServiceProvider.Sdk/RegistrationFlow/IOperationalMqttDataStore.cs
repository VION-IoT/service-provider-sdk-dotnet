namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    /// <summary>
    ///     Stores the operational MQTT data so it survives a process restart.
    /// </summary>
    public interface IOperationalMqttDataStore
    {
        /// <summary>
        ///     Returns the stored data, or <c>null</c> when nothing is stored or the stored content cannot be read back.
        /// </summary>
        OperationalData? Read();

        /// <summary>
        ///     Stores <paramref name="operationalData" />, replacing any previous value.
        /// </summary>
        /// <param name="operationalData">The operational MQTT data to store.</param>
        void Write(OperationalData operationalData);

        /// <summary>
        ///     Removes any stored data.
        /// </summary>
        void Clear();
    }
}
