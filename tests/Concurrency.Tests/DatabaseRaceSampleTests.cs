using LeaveWorkforce.Api.IntegrationTests.Infrastructure;
using LeaveWorkforce.Concurrency.Tests.Infrastructure;
using Npgsql;
using Xunit;

namespace LeaveWorkforce.Concurrency.Tests;

[Collection(ConcurrencyCollection.Name)]
public sealed class DatabaseRaceSampleTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string UniqueViolation = "23505";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() =>
        await RunSqlAsync(PostgresFixture.Migrator,
            "DROP TABLE IF EXISTS leave.race_scratch; CREATE TABLE leave.race_scratch (id uuid PRIMARY KEY)");

    public async ValueTask DisposeAsync() =>
        await RunSqlAsync(PostgresFixture.Migrator, "DROP TABLE IF EXISTS leave.race_scratch");

    [Fact]
    public async Task Two_operations_that_wait_for_each_other_finish_on_different_backends()
    {
        // Each operation opens its own connection and waits until the other has opened one. A serial run
        // could never get past the wait, so it would time out.
        var arrived = new CountdownEvent(2);
        Func<CancellationToken, Task<int>> Backend() => async token =>
        {
            await using var connection = new NpgsqlConnection(postgres.ConnectionString(PostgresFixture.AppRw));
            await connection.OpenAsync(token);
            arrived.Signal();
            await Task.Run(() => arrived.Wait(token), token);
            await using var command = new NpgsqlCommand("SELECT pg_backend_pid()", connection);
            return (int)(await command.ExecuteScalarAsync(token))!;
        };

        var outcomes = await ParallelRunner.RunAsync([Backend(), Backend()], TimeSpan.FromSeconds(20), Ct);

        Assert.All(outcomes, o => Assert.True(o.Succeeded, o.Exception?.ToString()));
        Assert.NotEqual(outcomes[0].Value, outcomes[1].Value);
    }

    [Fact]
    public async Task Two_inserts_of_the_same_unique_key_have_one_winner_and_one_23505_loser()
    {
        var id = Guid.NewGuid();
        Func<CancellationToken, Task<int>> Insert() => async token =>
        {
            await using var connection = new NpgsqlConnection(postgres.ConnectionString(PostgresFixture.AppRw));
            await connection.OpenAsync(token);
            await using var command = new NpgsqlCommand("INSERT INTO leave.race_scratch (id) VALUES (@id)", connection);
            command.Parameters.AddWithValue("id", id);
            return await command.ExecuteNonQueryAsync(token);
        };

        var outcomes = await ParallelRunner.RunAsync([Insert(), Insert()], cancellationToken: Ct);

        var winners = outcomes.Where(o => o.Succeeded).ToList();
        var losers = outcomes.Where(o => !o.Succeeded).ToList();
        Assert.Single(winners);
        Assert.Equal(1, winners[0].Value);
        var loser = Assert.Single(losers);
        Assert.False(loser.TimedOut);
        var failure = Assert.IsType<PostgresException>(loser.Exception);
        Assert.Equal(UniqueViolation, failure.SqlState);
        Assert.Equal(1, await CountAsync());
    }

    private async Task RunSqlAsync(string role, string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString(role));
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private async Task<long> CountAsync()
    {
        await using var connection = new NpgsqlConnection(postgres.AdminConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM leave.race_scratch", connection);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }
}
