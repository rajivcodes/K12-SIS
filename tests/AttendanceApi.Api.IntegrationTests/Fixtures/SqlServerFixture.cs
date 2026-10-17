using Testcontainers.MsSql;

namespace AttendanceApi.Api.IntegrationTests.Fixtures;

/// <summary>
/// One SQL Server 2022 container shared by every test in the <see cref="SqlServerCollection"/>.
/// Each test creates its own database in it, so tests don't see each other's data.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ServerConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SQL Server";
}
