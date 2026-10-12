using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace LeaveWorkforce.Api.IntegrationTests;

public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Get_health_returns_up()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"status":"up"}""", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Post_health_returns_405()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/health", content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_route_returns_404()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/nope", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
