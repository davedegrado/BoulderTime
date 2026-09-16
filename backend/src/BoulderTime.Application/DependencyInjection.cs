using BoulderTime.Application.Admin;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Grading;
using BoulderTime.Application.Candidates;
using BoulderTime.Application.Gyms;
using BoulderTime.Application.Staff;
using BoulderTime.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace BoulderTime.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<UserService>();
        services.AddScoped<GymAccess>();
        services.AddScoped<GymService>();
        services.AddScoped<SectorService>();
        services.AddScoped<StaffService>();
        services.AddScoped<InvitationService>();
        services.AddScoped<GymCandidateService>();
        services.AddScoped<AdminService>();
        services.AddScoped<GradingService>();
        services.AddScoped<BoulderService>();
        return services;
    }
}
