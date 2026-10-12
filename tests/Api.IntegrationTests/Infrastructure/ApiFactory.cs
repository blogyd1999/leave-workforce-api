using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace LeaveWorkforce.Api.IntegrationTests.Infrastructure;

/// <summary>The API host with <c>ConnectionStrings:App</c> pointing at the test container as app_rw.</summary>
public sealed class ApiFactory(PostgresFixture postgres) : WebApplicationFactory<Program>
{
    /// <summary>Lets tests tell the host's database sessions apart from every other connection.</summary>
    public const string ApplicationName = "leaveworkforce-api-test";

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:App"] = new Npgsql.NpgsqlConnectionStringBuilder(postgres.ConnectionString(PostgresFixture.AppRw))
                {
                    ApplicationName = ApplicationName,
                }.ConnectionString,
            }));
}
