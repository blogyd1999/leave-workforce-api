using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LeaveWorkforce.Audit.Contracts;

public static class AuditModule
{
    public static IServiceCollection AddAudit(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints) => endpoints;
}
