#if (HasSqlServer)
using Testcontainers.MsSql;
#elif (HasPostgreSql)
using Testcontainers.PostgreSql;
#else
using Testcontainers.MySql;
#endif
using DotNet.Testcontainers.Containers;

namespace Vertical_Slice_Clean_Architecture.Tests.Support;

/// <summary>
/// The database server the database integration tests run against, and the engine-specific details
/// the tests need (an unreachable connection string, the Dapper schema).
/// </summary>
/// <remarks>
/// <para>
/// By default a throwaway server is started in Docker with Testcontainers and removed afterwards.
/// To use an existing database instead (no Docker, or a CI service container), set the
/// <see cref="ConnectionStringVariable"/> environment variable to its connection string. Use an
/// empty, disposable database, never one with real data: the tests create the <c>Samples</c> table
/// (with EF Core, only when the database has no tables yet) and add rows with unique values.
/// </para>
/// </remarks>
internal sealed class TestDatabase : IAsyncDisposable
{
    /// <summary>
    /// The environment variable that, when set, supplies the connection string of an existing
    /// database to use instead of starting a container.
    /// </summary>
    public const string ConnectionStringVariable = "TEST_DATABASE_CONNECTION";

#if (HasSqlServer)
    /// <summary>
    /// A connection string for a port nothing listens on, so connecting fails quickly.
    /// </summary>
    public const string UnreachableConnectionString =
        "Server=127.0.0.1,1;Database=Sample;User ID=sa;Password=unused;Encrypt=False;Connect Timeout=3";

    /// <summary>
    /// The <c>Samples</c> table for Dapper, which has no migrations. Identifiers are unquoted to match the slices' SQL.
    /// </summary>
    public const string DapperSchema = """
        IF OBJECT_ID(N'Samples') IS NULL
        CREATE TABLE Samples (
            Id uniqueidentifier NOT NULL PRIMARY KEY,
            Name nvarchar(200) NOT NULL,
            Email nvarchar(320) NOT NULL UNIQUE,
            IsActive bit NOT NULL)
        """;
#elif (HasPostgreSql)
    /// <summary>
    /// A connection string for a port nothing listens on, so connecting fails quickly.
    /// </summary>
    public const string UnreachableConnectionString =
        "Host=127.0.0.1;Port=1;Database=sample;Username=unused;Password=unused;Timeout=3";

    /// <summary>
    /// The <c>Samples</c> table for Dapper, which has no migrations. Identifiers are unquoted to match the slices' SQL.
    /// </summary>
    public const string DapperSchema = """
        CREATE TABLE IF NOT EXISTS Samples (
            Id uuid NOT NULL PRIMARY KEY,
            Name varchar(200) NOT NULL,
            Email varchar(320) NOT NULL UNIQUE,
            IsActive boolean NOT NULL)
        """;
#else
    /// <summary>
    /// A connection string for a port nothing listens on, so connecting fails quickly.
    /// </summary>
    public const string UnreachableConnectionString =
        "Server=127.0.0.1;Port=1;Database=sample;User ID=unused;Password=unused;Connection Timeout=3";

    /// <summary>
    /// The <c>Samples</c> table for Dapper, which has no migrations. Identifiers are unquoted to match the slices' SQL.
    /// </summary>
    public const string DapperSchema = """
        CREATE TABLE IF NOT EXISTS Samples (
            Id CHAR(36) NOT NULL PRIMARY KEY,
            Name VARCHAR(200) NOT NULL,
            Email VARCHAR(320) NOT NULL UNIQUE,
            IsActive TINYINT(1) NOT NULL)
        """;
#endif

    /// <summary>
    /// The container started for this run, or <see langword="null"/> when an existing database is used.
    /// </summary>
    private readonly IDatabaseContainer? _container;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="connectionString">The connection string of the database to test against.</param>
    /// <param name="container">The container to dispose with this instance, if one was started.</param>
    private TestDatabase(string connectionString, IDatabaseContainer? container)
    {
        ConnectionString = connectionString;
        _container = container;
    }

    /// <summary>
    /// Gets the connection string of the database to test against.
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// Returns the database named by <see cref="ConnectionStringVariable"/>, or starts a container
    /// for this solution's engine.
    /// </summary>
    /// <returns>The running database.</returns>
    /// <exception cref="DockerUnavailableException">No connection string is set and Docker is not available.</exception>
    public static async Task<TestDatabase> StartAsync()
    {
        var existing = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return new TestDatabase(existing, container: null);
        }

#if (HasSqlServer)
        var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
#elif (HasPostgreSql)
        var container = new PostgreSqlBuilder("postgres:17-alpine").Build();
#else
        // 8.4 matches Database:MySqlServerVersion in appsettings.json.
        var container = new MySqlBuilder("mysql:8.4").Build();
#endif
        await container.StartAsync();
        return new TestDatabase(container.GetConnectionString(), container);
    }

    /// <summary>
    /// Stops and removes the container, if one was started.
    /// </summary>
    /// <returns>A task that completes when the container is gone.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
