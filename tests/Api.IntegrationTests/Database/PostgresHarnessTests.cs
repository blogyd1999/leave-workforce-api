using System.Net;
using LeaveWorkforce.Api.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace LeaveWorkforce.Api.IntegrationTests.Database;

[Collection(PostgresCollection.Name)]
public sealed class PostgresHarnessTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly string[] Roles = [PostgresFixture.Migrator, PostgresFixture.AppRw, PostgresFixture.RetentionJob];

    private async Task<T?> ScalarAsync<T>(string role, string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString(role));
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        return (T?)await command.ExecuteScalarAsync(Ct);
    }

    private Task ExecuteAsync(string role, string sql) => ScalarAsync<object>(role, sql);

    private async Task AssertDeniedAsync(string role, string sql, params string[] acceptedMessageParts)
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(role, sql));
        Assert.Contains(acceptedMessageParts, part => ex.MessageText.Contains(part, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Container_runs_the_pinned_image_with_postgres_18()
    {
        Assert.StartsWith("postgres:", postgres.Image);
        var version = await ScalarAsync<string>(PostgresFixture.Superuser, "SHOW server_version");
        Assert.StartsWith("18", version);
    }

    [Fact]
    public async Task Roles_exist_with_login_only_and_no_elevated_attributes()
    {
        var count = await ScalarAsync<long>(PostgresFixture.Superuser,
            "SELECT count(*) FROM pg_roles WHERE rolname IN ('migrator','app_rw','retention_job') "
            + "AND rolcanlogin AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole");
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task Schemas_are_owned_by_migrator()
    {
        var count = await ScalarAsync<long>(PostgresFixture.Superuser,
            "SELECT count(*) FROM pg_namespace n JOIN pg_roles r ON r.oid = n.nspowner "
            + "WHERE n.nspname IN ('people','leave','audit') AND r.rolname = 'migrator'");
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task Each_role_connects_with_its_password()
    {
        foreach (var role in Roles)
        {
            Assert.Equal(1, await ScalarAsync<int>(role, "SELECT 1"));
        }
    }

    [Fact]
    public async Task A_wrong_password_is_refused()
    {
        foreach (var role in Roles)
        {
            await using var connection = new NpgsqlConnection(postgres.ConnectionStringWithWrongPassword(role));
            var ex = await Assert.ThrowsAsync<PostgresException>(() => connection.OpenAsync(Ct));
            Assert.Equal(PostgresErrorCodes.InvalidPassword, ex.SqlState);
        }
    }

    [Fact]
    public async Task App_rw_cannot_create_objects_or_install_extensions()
    {
        await AssertDeniedAsync(PostgresFixture.AppRw, "CREATE TABLE people.htmp_denied (id int)", "permission denied");
        await AssertDeniedAsync(PostgresFixture.AppRw, "CREATE TABLE public.htmp_denied (id int)", "permission denied");
        await AssertDeniedAsync(PostgresFixture.AppRw, "CREATE TEMP TABLE htmp_denied (id int)", "permission denied");
        await AssertDeniedAsync(PostgresFixture.AppRw, "CREATE EXTENSION IF NOT EXISTS btree_gist",
            "permission denied", "no schema has been selected");
    }

    [Fact]
    public async Task Public_has_no_database_access_and_runtime_roles_have_no_public_schema_rights()
    {
        var publicDb = await ScalarAsync<long>(PostgresFixture.Superuser,
            "SELECT count(*) FROM aclexplode((SELECT datacl FROM pg_database WHERE datname = current_database())) WHERE grantee = 0");
        Assert.Equal(0, publicDb);

        var publicSchema = await ScalarAsync<long>(PostgresFixture.Superuser,
            "SELECT count(*) FROM (VALUES ('app_rw'),('retention_job')) r(n) "
            + "WHERE has_schema_privilege(r.n, 'public', 'USAGE') OR has_schema_privilege(r.n, 'public', 'CREATE')");
        Assert.Equal(0, publicSchema);
    }

    [Fact]
    public async Task Migrator_can_install_an_extension_but_the_change_is_rolled_back()
    {
        await ExecuteAsync(PostgresFixture.Migrator, "BEGIN; CREATE EXTENSION IF NOT EXISTS btree_gist; ROLLBACK;");
    }

    [Fact]
    public async Task Default_privileges_match_the_approved_boundaries()
    {
        const string drop = "DROP TABLE IF EXISTS people.htmp, leave.htmp, audit.htmp";
        try
        {
            await ExecuteAsync(PostgresFixture.Migrator,
                "CREATE TABLE people.htmp (id int); CREATE TABLE leave.htmp (id int); CREATE TABLE audit.htmp (id int)");

            foreach (var schema in new[] { "people", "leave" })
            {
                await ExecuteAsync(PostgresFixture.AppRw,
                    $"INSERT INTO {schema}.htmp VALUES (1); SELECT * FROM {schema}.htmp; UPDATE {schema}.htmp SET id = 2; DELETE FROM {schema}.htmp");
            }

            await ExecuteAsync(PostgresFixture.AppRw, "INSERT INTO audit.htmp VALUES (1); SELECT * FROM audit.htmp");
            await AssertDeniedAsync(PostgresFixture.AppRw, "UPDATE audit.htmp SET id = 2", "permission denied");
            await AssertDeniedAsync(PostgresFixture.AppRw, "DELETE FROM audit.htmp", "permission denied");

            await ExecuteAsync(PostgresFixture.RetentionJob, "SELECT * FROM audit.htmp; DELETE FROM audit.htmp");
            await AssertDeniedAsync(PostgresFixture.RetentionJob, "INSERT INTO audit.htmp VALUES (1)", "permission denied");
            await AssertDeniedAsync(PostgresFixture.RetentionJob, "UPDATE audit.htmp SET id = 2", "permission denied");
            await AssertDeniedAsync(PostgresFixture.RetentionJob, "SELECT * FROM people.htmp", "permission denied");
        }
        finally
        {
            await ExecuteAsync(PostgresFixture.Migrator, drop);
        }
    }

    [Fact]
    public async Task Roles_script_can_be_rerun_and_roles_still_work()
    {
        await postgres.RunRolesScriptAsync();

        foreach (var role in Roles)
        {
            Assert.Equal(1, await ScalarAsync<int>(role, "SELECT 1"));
        }
    }

    [Fact]
    public async Task Health_through_the_factory_returns_up_without_the_api_connecting()
    {
        await using var factory = new ApiFactory(postgres);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"status":"up"}""", await response.Content.ReadAsStringAsync(Ct));

        // ConnectionStrings:App reached the host as app_rw with the test application name. Not printed: it holds a password.
        var configured = factory.Services.GetRequiredService<IConfiguration>()["ConnectionStrings:App"];
        Assert.True(configured?.Contains("Username=app_rw", StringComparison.Ordinal) == true
            && configured.Contains($"Application Name={ApiFactory.ApplicationName}", StringComparison.Ordinal),
            "ConnectionStrings:App was not set to the app_rw test connection string.");

        // The host never connected: no session carries its application name.
        Assert.Equal(0, await ScalarAsync<long>(PostgresFixture.Superuser,
            $"SELECT count(*) FROM pg_stat_activity WHERE application_name = '{ApiFactory.ApplicationName}'"));
    }
}
