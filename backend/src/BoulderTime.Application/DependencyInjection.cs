using BoulderTime.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace BoulderTime.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<UserService>();
        return services;
    }
}
