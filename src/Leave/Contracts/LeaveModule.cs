using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LeaveWorkforce.Leave.Contracts;

public static class LeaveModule
{
    public static IServiceCollection AddLeave(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapLeaveEndpoints(this IEndpointRouteBuilder endpoints) => endpoints;
}
