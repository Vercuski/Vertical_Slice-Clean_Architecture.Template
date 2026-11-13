using Microsoft.Data.Sqlite;
#if (HasEfCore)
using Microsoft.EntityFrameworkCore;
#endif
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data;
using System.Data.Common;
using Vertical_Slice_Clean_Architecture.Infrastructure.HealthChecks;
using Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Options;
using Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Providers;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Vertical_Slice_Clean_Architecture.Tests.UnitTests;

/// <summary>
/// Unit tests for <see cref="DatabaseHealthCheck"/>. They use real ADO.NET connections from SQLite
/// (an in-memory database for a reachable server, a path that cannot be opened for an unreachable
/// one) and a fake connection that never finishes opening, so they need no database server.
/// </summary>
[TestFixture]
public class DatabaseHealthCheckTests
{
    /// <summary>
    /// A connection string SQLite can open.
    /// </summary>
    private const string Reachable = "Data Source=:memory:";

    /// <summary>
    /// A connection string SQLite cannot open, so opening fails with a <see cref="DbException"/>.
    /// </summary>
    private const string Unreachable = "Data Source=/no/such/directory/health.db;Mode=ReadOnly";

    /// <summary>
    /// A connection string for which the test provider returns a connection that never opens.
    /// </summary>
    private const string Hangs = "hangs";

    /// <summary>
    /// Both databases answer: the result is healthy and names the engine.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CheckHealthAsync_Should_BeHealthy_WhenBothDatabasesAnswer()
    {
        var result = await RunAsync(query: Reachable, command: Reachable);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
            Assert.That(result.Data["engine"], Is.EqualTo(TestDatabaseProvider.EngineName));
            Assert.That(result.Data["commandDatabase"], Is.EqualTo("Reachable"));
            Assert.That(result.Data["queryDatabase"], Is.EqualTo("Reachable"));
        }
    }

    /// <summary>
    /// Either side failing makes the check unhealthy, and the description names that side.
    /// </summary>
    /// <param name="query">The query connection string.</param>
    /// <param name="command">The command connection string.</param>
    /// <param name="expectedDescription">The expected description.</param>
    [TestCase(Unreachable, Reachable, "The query database is not reachable.")]
    [TestCase(Reachable, Unreachable, "The command database is not reachable.")]
    [TestCase(Unreachable, Unreachable, "Neither database is reachable.")]
    public async Task CheckHealthAsync_Should_BeUnhealthy_WhenADatabaseFails(string query, string command, string expectedDescription)
    {
        var result = await RunAsync(query, command);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
            Assert.That(result.Description, Is.EqualTo(expectedDescription));
        }
    }

    /// <summary>
    /// A server that accepts the connection but never answers must not hold the check past its
    /// timeout, even though the connection ignores cancellation (as real drivers do while opening).
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CheckHealthAsync_Should_StopWaiting_WhenItsTimeoutElapses()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var check = RunAsync(query: Hangs, command: Reachable, timeout.Token);

        // If the check waited for the connection, it would never finish; fail instead of hanging the run.
        var finished = await Task.WhenAny(check, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.That(finished, Is.SameAs(check), "The health check kept waiting after its timeout elapsed.");
        Assert.That(async () => await check, Throws.InstanceOf<OperationCanceledException>());
    }

    /// <summary>
    /// Runs a <see cref="DatabaseHealthCheck"/> over the given connection strings.
    /// </summary>
    /// <param name="query">The query connection string.</param>
    /// <param name="command">The command connection string.</param>
    /// <param name="cancellationToken">Stands in for the registration timeout.</param>
    /// <returns>The check's result.</returns>
    private static Task<HealthCheckResult> RunAsync(string query, string command, CancellationToken cancellationToken = default)
    {
        var options = MsOptions.Create(new ConnectionStringOptions { QueryDbConnection = query, CommandDbConnection = command });
        var check = new DatabaseHealthCheck(options, new TestDatabaseProvider(), NullLogger<DatabaseHealthCheck>.Instance);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("DatabaseHealthCheck", check, HealthStatus.Unhealthy, tags: null),
        };

        return check.CheckHealthAsync(context, cancellationToken);
    }

    /// <summary>
    /// Creates SQLite connections, or a <see cref="HangingConnection"/> for <see cref="Hangs"/>.
    /// </summary>
    private sealed class TestDatabaseProvider : IDatabaseProvider
    {
        /// <summary>
        /// The engine name the provider reports.
        /// </summary>
        public const string EngineName = "Test SQLite";

        /// <inheritdoc />
        public string Name => EngineName;
#if (HasEfCore)

        /// <inheritdoc />
        public void ConfigureEfCore(DbContextOptionsBuilder optionsBuilder, string connectionString)
        {
            throw new NotSupportedException("The health check does not use EF Core.");
        }
#endif

        /// <inheritdoc />
        public DbConnection CreateConnection(string connectionString)
        {
            return connectionString == Hangs ? new HangingConnection() : new SqliteConnection(connectionString);
        }
    }

    /// <summary>
    /// A connection whose <see cref="OpenAsync"/> never completes and ignores cancellation, like a
    /// driver stuck in a handshake with a server that never answers.
    /// </summary>
    private sealed class HangingConnection : DbConnection
    {
        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString { get; set; } = Hangs;

        /// <inheritdoc />
        public override string Database => string.Empty;

        /// <inheritdoc />
        public override string DataSource => string.Empty;

        /// <inheritdoc />
        public override string ServerVersion => string.Empty;

        /// <inheritdoc />
        public override ConnectionState State => ConnectionState.Connecting;

        /// <inheritdoc />
        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            return new TaskCompletionSource().Task;
        }

        /// <inheritdoc />
        public override void Open()
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Close()
        {
        }

        /// <inheritdoc />
        public override void ChangeDatabase(string databaseName)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        protected override DbCommand CreateDbCommand()
        {
            throw new NotSupportedException();
        }
    }
}
