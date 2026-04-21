using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorEventBus;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for registering
/// <see cref="IEventBus"/>.
/// </summary>
public static class BlazorEventBusServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IEventBus"/> as a <em>scoped</em> service, giving each
    /// Blazor Server circuit — and each Blazor WebAssembly application — its own
    /// isolated event bus.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddBlazorEventBus(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IEventBus, EventBus>();
        return services;
    }
}