using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MQTTnet;
using Vion.ServiceProvider.Sdk.RegistrationFlow;
using Vion.ServiceProvider.Sdk.SystemControl;

namespace Vion.ServiceProvider.Sdk
{
    /// <summary>
    ///     Optional DI registration for the service provider SDK. Convenience for the hosted/DI path — it does not replace
    ///     the raw <see cref="ServiceProviderClientConfigurationBuilder" />, which remains for authors who want full control.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        ///     Registers a <see cref="ServiceProviderClient" /> (and <see cref="IServiceProviderClient" />), wires the default
        ///     system-control handlers, and seeds <see cref="LogLevelManager" /> from configuration.
        /// </summary>
        /// <remarks>
        ///     Unless the supplied configuration overrides them, the <c>restart</c> handler is wired to the host-stopping
        ///     <see cref="RestartHandler" /> and the <c>logLevel/set</c> handler to <see cref="SetLogLevelHandler" /> — both
        ///     resolved from DI (so <see cref="RestartHandler" /> receives the host's
        ///     <see cref="Microsoft.Extensions.Hosting.IHostApplicationLifetime" />). The state publisher / store
        ///     (<see cref="Services.ServiceStatePublisher" /> / <see cref="Services.ServiceStateStore{TService}" />) are
        ///     opt-in: construct them in the post-acceptance <c>WithHandlers</c> callback, where the installation topic is known.
        /// </remarks>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">Configuration used to seed <see cref="LogLevelManager" />.</param>
        /// <param name="configure">
        ///     Builds the client configuration, resolving services from the provided <see cref="IServiceProvider" /> as needed
        ///     (e.g. for handler construction).
        /// </param>
        /// <returns>The service collection, for chaining.</returns>
        public static IServiceCollection AddVionServiceProviderSdk(this IServiceCollection services,
                                                                   IConfiguration configuration,
                                                                   Func<IServiceProvider, ServiceProviderClientConfiguration> configure)
        {
            LogLevelManager.InitializeFromConfig(configuration);

            services.AddSingleton<RestartHandler>();
            services.AddSingleton<SetLogLevelHandler>();

            services.AddSingleton<ServiceProviderClient>(serviceProvider =>
                                                         {
                                                             var clientConfiguration = configure(serviceProvider);
                                                             var restartHandler = serviceProvider.GetRequiredService<RestartHandler>();
                                                             clientConfiguration.OnRestartCallback ??= restartHandler.HandleAsync;
                                                             var setLogLevelHandler = serviceProvider.GetRequiredService<SetLogLevelHandler>();
                                                             clientConfiguration.OnLogLevelChangeCallback ??= setLogLevelHandler.HandleAsync;

                                                             var factory = serviceProvider.GetService<MqttClientFactory>() ?? new MqttClientFactory();
                                                             var logger = serviceProvider.GetRequiredService<ILogger<ServiceProviderClient>>();
                                                             return new ServiceProviderClient(clientConfiguration, factory, logger);
                                                         });
            services.AddSingleton<IServiceProviderClient>(serviceProvider => serviceProvider.GetRequiredService<ServiceProviderClient>());

            return services;
        }
    }
}