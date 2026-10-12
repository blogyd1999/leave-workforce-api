using LeaveWorkforce.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace LeaveWorkforce.Concurrency.Tests.Infrastructure;

/// <summary>Shares one PostgreSQL container (the unchanged Api.IntegrationTests fixture) across the concurrency tests.</summary>
[CollectionDefinition(Name)]
public sealed class ConcurrencyCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Concurrency";
}
