namespace Vion.ServiceProvider.Sdk.RegistrationFlow
{
    /// <summary>
    ///     Stores the operational credentials so they survive a process restart.
    /// </summary>
    public interface IOperationalCredentialsStore
    {
        /// <summary>
        ///     Returns the stored credentials, or <c>null</c> when nothing is stored or the stored content cannot be read back.
        /// </summary>
        OperationalData? Read();

        /// <summary>
        ///     Stores <paramref name="operationalData" />, replacing any previous value.
        /// </summary>
        /// <param name="operationalData">The credentials to store.</param>
        void Write(OperationalData operationalData);

        /// <summary>
        ///     Removes any stored credentials.
        /// </summary>
        void Clear();
    }
}
