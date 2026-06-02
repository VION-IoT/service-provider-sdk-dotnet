using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Vion.ServiceProvider.Sdk.Infrastructure
{
    /// <summary>Default <see cref="IDiskAccessProvider" /> backed by <see cref="System.IO.File" /> / <see cref="Directory" />.</summary>
    [ExcludeFromCodeCoverage]
    public sealed class DiskAccessProvider : IDiskAccessProvider
    {
        /// <inheritdoc />
        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        /// <inheritdoc />
        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        /// <inheritdoc />
        public string ReadAllText(string path)
        {
            return File.ReadAllText(path);
        }

        /// <inheritdoc />
        public void WriteAllText(string path, string contents)
        {
            File.WriteAllText(path, contents);
        }
    }
}