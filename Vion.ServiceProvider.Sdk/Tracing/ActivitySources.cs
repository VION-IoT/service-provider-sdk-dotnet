using System.Diagnostics;

namespace Vion.ServiceProvider.Sdk.Tracing
{
    public static class ActivitySources
    {
        public static readonly ActivitySource Messaging = new("Messaging");
    }
}