using System;

using Splat;

namespace Resona.UI.ViewModels
{
    public static class ReadonlyDependencyResolverExtensions
    {
        public static T GetRequiredService<T>(this IReadonlyDependencyResolver resolver)
        {
            return resolver.GetService<T>() ?? throw new Exception($"Type {typeof(T).Name} not registered");
        }
    }
}
