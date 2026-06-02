namespace Vion.ServiceProvider.Sdk.Infrastructure
{
    /// <summary>
    ///     Abstraction over the file system.
    /// </summary>
    public interface IDiskAccessProvider
    {
        /// <summary>Creates the specified directory if it does not exist.</summary>
        /// <param name="path">The directory path to create.</param>
        void CreateDirectory(string path);

        /// <summary>Returns whether the file exists at the given path.</summary>
        /// <param name="path">The file path to check.</param>
        bool FileExists(string path);

        /// <summary>Reads the file as text.</summary>
        /// <param name="path">The file path to read.</param>
        string ReadAllText(string path);

        /// <summary>Writes the text to the file, creating or replacing.</summary>
        /// <param name="path">The file path to write to.</param>
        /// <param name="contents">The contents to write.</param>
        void WriteAllText(string path, string contents);
    }
}