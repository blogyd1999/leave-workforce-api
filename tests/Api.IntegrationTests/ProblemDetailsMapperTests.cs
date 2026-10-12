using System.Net;
using System.Text.Json;
using LeaveWorkforce.BuildingBlocks.Http;
using LeaveWorkforce.BuildingBlocks.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LeaveWorkforce.Api.IntegrationTests;

public sealed class ProblemDetailsMapperTests
{
    private const string Secret = "super-secret-connection-string";

    [Theory]
    [InlineData("unauthenticated", HttpStatusCode.Unauthorized, "Unauthenticated")]
    [InlineData("throttled", (HttpStatusCode)429, "Too many requests")]
    [InlineData("forbidden", HttpStatusCode.Forbidden, "Forbidden")]
    [InlineData("not-found", HttpStatusCode.NotFound, "Not found")]
    [InlineData("validation", HttpStatusCode.BadRequest, "Validation failed")]
    [InlineData("conflict", HttpStatusCode.Conflict, "Conflict")]
    public async Task Each_kind_maps_to_its_status_title_and_code(string route, HttpStatusCode status, string title)
    {
        await using var app = await StartTestHostAsync();
        using var client = app.GetTestClient();

        using var response = await client.GetAsync("/" + route, TestContext.Current.CancellationToken);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = await ReadJsonAsync(response);
        Assert.Equal((int)status, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(title, body.RootElement.GetProperty("title").GetString());
        Assert.Equal("test_code", body.RootElement.GetProperty("code").GetString());
        Assert.False(body.RootElement.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Validation_with_field_errors_has_errors_map()
    {
        await using var app = await StartTestHostAsync();
        using var client = app.GetTestClient();

        using var response = await client.GetAsync("/validation-fields", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        var messages = body.RootElement.GetProperty("errors").GetProperty("reason");
        Assert.Equal("required", messages[0].GetString());
        Assert.Equal("Fix the reason.", body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Lock_timeout_conflict_carries_code()
    {
        await using var app = await StartTestHostAsync();
        using var client = app.GetTestClient();

        using var response = await client.GetAsync("/lock-timeout", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal("lock_timeout", body.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Unhandled_exception_returns_generic_500_without_internals(string environment)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureServices(services => services.AddTransient<IStartupFilter, ThrowingEndpointFilter>());
        });
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/boom", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(text);
        Assert.Equal("internal_error", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(500, body.RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", text, StringComparison.Ordinal);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

    private static async Task<WebApplication> StartTestHostAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();

        app.MapGet("/unauthenticated", () => Error.Unauthenticated("test_code").ToProblem());
        app.MapGet("/throttled", () => Error.Throttled("test_code").ToProblem());
        app.MapGet("/forbidden", () => Error.Forbidden("test_code").ToProblem());
        app.MapGet("/not-found", () => Error.NotFound("test_code").ToProblem());
        app.MapGet("/validation", () => Error.Validation("test_code").ToProblem());
        app.MapGet("/conflict", () => Error.Conflict("test_code").ToProblem());
        app.MapGet("/validation-fields", () => Error.Validation(
            "invalid_input",
            "Fix the reason.",
            new Dictionary<string, string[]> { ["reason"] = ["required"] }).ToProblem());
        app.MapGet("/lock-timeout", () => Error.Conflict(ErrorCodes.LockTimeout).ToProblem());

        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private sealed class ThrowingEndpointFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            // Added after the real pipeline so the endpoint sits behind the Api's exception handler.
            next(app);
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapGet(
                "/boom",
                (Func<string>)(() => throw new InvalidOperationException(Secret))));
        };
    }
}
