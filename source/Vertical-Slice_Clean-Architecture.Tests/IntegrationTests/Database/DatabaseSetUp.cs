using DotNet.Testcontainers.Builders;
#if (HasEfCore)
using Microsoft.EntityFrameworkCore;
#endif
using Microsoft.Extensions.DependencyInjection;
#if (HasEfCore)
using Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Contexts;
#else
using Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Providers;
#endif
using Vertical_Slice_Clean_Architecture.Tests.Support;

namespace Vertical_Slice_Clean_Architecture.Tests.IntegrationTests.Database;

/// <summary>
/// Starts the database once for every test in this namespace, creates the schema, and hosts the
/// API against it.
/// </summary>
/// <remarks>
/// If neither <see cref="TestDatabase.ConnectionStringVariable"/> is set nor Docker is available,
/// the database tests are reported as ignored (with the reason) rather than failed.
/// </remarks>
[SetUpFixture]
public class DatabaseSetUp
{
    /// <summary>
    /// The database the tests run against.
    /// </summary>
    private static TestDatabase? _database;

    /// <summary>
    /// Gets the API host connected to the test database.
    /// </summary>
    internal static ApiFactory Factory { get; private set; } = null!;

    /// <summary>
    /// Starts the database, hosts the API against it, and creates the <c>Samples</c> table.
    /// </summary>
    /// <returns>A task representing the asynchronous setup.</returns>
    [OneTimeSetUp]
    public async Task StartDatabaseAsync()
    {
        try
        {
            _database = await TestDatabase.StartAsync();
        }
        catch (DockerUnavailableException exception)
        {
            Assert.Ignore(
                $"Database tests need Docker, or {TestDatabase.ConnectionStringVariable} set to a test database. {exception.Message}");
        }

        Factory = new ApiFactory(_database!.ConnectionString);

        using var scope = Factory.Services.CreateScope();
#if (HasEfCore)
        // The schema comes from the EF Core model. Production databases use migrations instead.
        await scope.ServiceProvider.GetRequiredService<CommandDbContext>().Database.EnsureCreatedAsync();
#else
        var provider = scope.ServiceProvider.GetRequiredService<IDatabaseProvider>();
        await using var connection = provider.CreateConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = TestDatabase.DapperSchema;
        await command.ExecuteNonQueryAsync();
#endif
    }

    /// <summary>
    /// Stops the host and removes the database container.
    /// </summary>
    /// <returns>A task representing the asynchronous teardown.</returns>
    [OneTimeTearDown]
    public async Task StopDatabaseAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }

        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }
}
