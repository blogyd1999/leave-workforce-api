using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LeaveWorkforce.People.Contracts;

public static class PeopleModule
{
    public static IServiceCollection AddPeople(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapPeopleEndpoints(this IEndpointRouteBuilder endpoints) => endpoints;
}
