using System;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;

using Splat;

namespace Resona.UI
{
    /// <summary>
    /// Exposes an already built <see cref="IServiceProvider"/> through Splat, so views and ReactiveUI can resolve the
    /// app's services from <see cref="Locator"/>.
    /// </summary>
    /// <remarks>
    /// Splat's own Microsoft DI resolver rebuilds its provider (disposing every singleton created so far) whenever
    /// anything registers with it, and ReactiveUI registers during Avalonia setup. Building the provider once and
    /// handing Splat factories that point at it keeps a single set of singletons no matter when they are first
    /// resolved or what registers with Splat afterwards.
    /// </remarks>
    public static class SplatServiceBridge
    {
        public static void Register(IServiceCollection services, IServiceProvider provider, IMutableDependencyResolver resolver)
        {
            foreach (var serviceType in services.Select(d => d.ServiceType).Where(t => !t.IsGenericTypeDefinition).Distinct())
            {
                resolver.Register(() => provider.GetService(serviceType), serviceType);
            }
        }
    }
}
