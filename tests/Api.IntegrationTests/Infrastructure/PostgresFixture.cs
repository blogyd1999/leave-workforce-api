using System.Security.Cryptography;
using DotNet.Testcontainers.Builders;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace LeaveWorkforce.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Starts a disposable PostgreSQL container from the image pinned in deploy/postgres.env, applies
/// deploy/db/roles.sql with random test-only credentials, and exposes TCP connection strings per role.
/// One container per collection; Testcontainers (and its Ryuk reaper) removes it afterwards.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string Database = "leave_test";
    public const string Superuser = "postgres";
    public const string Migrator = "migrator";
    public const string AppRw = "app_rw";
    public const string RetentionJob = "retention_job";

    private const string RolesScriptPathInContainer = "/tmp/roles.sql";

    private readonly string _superuserPassword = NewPassword();
    private readonly Dictionary<string, string> _rolePasswords = new()
    {
        [Migrator] = NewPassword(),
        [AppRw] = NewPassword(),
        [RetentionJob] = NewPassword(),
    };

    private PostgreSqlContainer? _container;

    public string Image { get; private set; } = "";

    /// <summary>Connection string for a role (or the superuser) over the mapped TCP port, so passwords are checked.</summary>
    public string ConnectionString(string role) => Build(role, PasswordOf(role));

    /// <summary>Connection string for a role with a password that is not the role's own.</summary>
    public string ConnectionStringWithWrongPassword(string role) => Build(role, NewPassword());

    public string AdminConnectionString => ConnectionString(Superuser);

    public async ValueTask InitializeAsync()
    {
        try
        {
            await StartAsync();
        }
        catch
        {
            // xunit may not call DisposeAsync after a failed InitializeAsync, so never leave a container behind.
            await DisposeAsync();
            throw;
        }
    }

    private async ValueTask StartAsync()
    {
        Image = ReadImagePin();
        var rolesScript = await File.ReadAllBytesAsync(Path.Combine(RepositoryRoot(), "deploy", "db", "roles.sql"));

        try
        {
            _container = new PostgreSqlBuilder(Image)
                .WithDatabase(Database)
                .WithUsername(Superuser)
                .WithPassword(_superuserPassword)
                .WithEnvironment("MIGRATOR_PASSWORD", _rolePasswords[Migrator])
                .WithEnvironment("APP_RW_PASSWORD", _rolePasswords[AppRw])
                .WithEnvironment("RETENTION_JOB_PASSWORD", _rolePasswords[RetentionJob])
                // The image first runs a temporary socket-only server for initialisation; waiting on TCP
                // makes sure we only continue once the real server is listening.
                .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted(
                    "pg_isready", "-h", "127.0.0.1", "-U", Superuser, "-d", Database))
                .WithResourceMapping(rolesScript, RolesScriptPathInContainer)
                .Build();
            await _container.StartAsync(TestContext.Current.CancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "Could not start the PostgreSQL test container. Is Docker running and reachable "
                + "(for Colima, set DOCKER_HOST=unix://$HOME/.colima/docker.sock)? " + Redact(ex.Message), ex);
        }

        await WaitForHostConnectivityAsync();
        await RunRolesScriptAsync();
    }

    // With a forwarded Docker socket (Colima) the mapped port can lag behind the container's readiness.
    private async Task WaitForHostConnectivityAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            try
            {
                await using var connection = new NpgsqlConnection(AdminConnectionString);
                await connection.OpenAsync(ct);
                return;
            }
            catch (NpgsqlException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(200, ct);
            }
        }
    }

    /// <summary>Applies the unchanged roles.sql with plain psql inside the container; safe to call again.</summary>
    public async Task RunRolesScriptAsync()
    {
        var result = await _container!.ExecAsync(
            ["psql", "-U", Superuser, "-d", Database, "-v", "ON_ERROR_STOP=1", "-f", RolesScriptPathInContainer],
            TestContext.Current.CancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"roles.sql failed with exit code {result.ExitCode}: {Redact(result.Stderr)}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private string Build(string role, string password) => new NpgsqlConnectionStringBuilder
    {
        Host = _container!.Hostname,
        Port = _container.GetMappedPublicPort(5432),
        Database = Database,
        Username = role,
        Password = password,
        Pooling = false,
    }.ConnectionString;

    private string PasswordOf(string role) =>
        role == Superuser ? _superuserPassword : _rolePasswords[role];

    private string Redact(string text)
    {
        foreach (var secret in _rolePasswords.Values.Append(_superuserPassword))
        {
            text = text.Replace(secret, "***", StringComparison.Ordinal);
        }
        return text;
    }

    private static string NewPassword() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    private static string ReadImagePin()
    {
        var path = Path.Combine(RepositoryRoot(), "deploy", "postgres.env");
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("POSTGRES_IMAGE=", StringComparison.Ordinal))
            {
                var value = trimmed["POSTGRES_IMAGE=".Length..].Trim().Trim('"', '\'');
                if (value.Length > 0)
                {
                    return value;
                }
            }
        }
        throw new InvalidOperationException($"POSTGRES_IMAGE is not set in {path}.");
    }

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LeaveWorkforce.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName
            ?? throw new InvalidOperationException("Could not find LeaveWorkforce.slnx above the test output folder.");
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
