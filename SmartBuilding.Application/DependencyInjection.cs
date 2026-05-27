using Microsoft.Extensions.DependencyInjection;
using SmartBuilding.Application.Interfaces;

namespace SmartBuilding.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Interfaces implemented in Infrastructure — registered there
        return services;
    }
}
