using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data.Common;
using Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Options;
using Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Providers;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.HealthChecks;

/// <summary>
/// Reports whether the application can reach both of its databases: the command (write) database
/// and the query (read) database.
/// </summary>
/// <remarks>
/// <para>
/// Discovered and registered automatically by the Infrastructure health check assembly scan, with
/// the scan's timeout, and reported on <c>/health</c> (not on <c>/health/live</c>). The result is
/// <see cref="HealthStatus.Healthy"/> only when both databases answer; if either does not, it is
/// <see cref="HealthStatus.Unhealthy"/>, because the slices that use that side would fail.
/// </para>
/// <para>
/// It opens its own short-lived ADO.NET connections through <see cref="IDatabaseProvider"/> rather
/// than using a DbContext or the slices' connection factories, so it works the same with EF Core
/// and Dapper, and a probe abandoned at the timeout never leaves a request's DbContext with a
/// connection still opening.
/// </para>
/// <para>
/// Its data names the engine and the state of each side, for example
/// <c>{ "engine": "PostgreSQL", "commandDatabase": "Reachable", "queryDatabase": "Unreachable" }</c>.
/// Connection errors are logged, not returned, because they can reveal server names.
/// </para>
/// </remarks>
/// <param name="connectionStringOptions">The query and command connection strings.</param>
/// <param name="databaseProvider">Creates connections for the configured engine.</param>
/// <param name="logger">Records the connection error when a database cannot be reached.</param>
internal sealed class DatabaseHealthCheck(
    IOptions<ConnectionStringOptions> connectionStringOptions,
    IDatabaseProvider databaseProvider,
    ILogger<DatabaseHealthCheck> logger) : IHealthCheck
{
    /// <summary>
    /// Connects to both databases, in parallel, and reports the combined result.
    /// </summary>
    /// <param name="context">Context for the health check registration being evaluated.</param>
    /// <param name="cancellationToken">Cancelled when the check exceeds its registration timeout.</param>
    /// <returns>
    /// <see cref="HealthStatus.Healthy"/> when both databases answer; otherwise
    /// <see cref="HealthStatus.Unhealthy"/> (the registration's failure status).
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// The registration timeout elapsed; the health check service reports it as a timeout.
    /// </exception>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var connectionStrings = connectionStringOptions.Value;
        var command = CanConnectAsync(connectionStrings.CommandDbConnection, "command", cancellationToken);
        var query = CanConnectAsync(connectionStrings.QueryDbConnection, "query", cancellationToken);
        var both = Task.WhenAll(command, query);

        try
        {
            // Database drivers do not all honour cancellation while opening a connection (a server
            // that accepts the connection but never answers holds them until their own connect
            // timeout). WaitAsync stops waiting when the token is cancelled, so the registration
            // timeout is what bounds /health. Each probe still disposes its connection when it ends.
            await both.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Observe the abandoned probes' eventual failure so it is not reported as unobserved.
            _ = both.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            throw;
        }

        var commandReachable = command.Result;
        var queryReachable = query.Result;
        var data = new Dictionary<string, object>
        {
            ["engine"] = databaseProvider.Name,
            ["commandDatabase"] = commandReachable ? "Reachable" : "Unreachable",
            ["queryDatabase"] = queryReachable ? "Reachable" : "Unreachable",
        };

        return (commandReachable, queryReachable) switch
        {
            (true, true) => HealthCheckResult.Healthy("Both databases are reachable.", data),
            (false, false) => new HealthCheckResult(context.Registration.FailureStatus, "Neither database is reachable.", data: data),
            (false, true) => new HealthCheckResult(context.Registration.FailureStatus, "The command database is not reachable.", data: data),
            (true, false) => new HealthCheckResult(context.Registration.FailureStatus, "The query database is not reachable.", data: data),
        };
    }

    /// <summary>
    /// Opens a new connection, runs <c>SELECT 1</c> (valid on every supported engine), and disposes it.
    /// A database error is logged as a warning.
    /// </summary>
    /// <param name="connectionString">The connection string of the database to probe.</param>
    /// <param name="side">"command" or "query"; used only in the log message.</param>
    /// <param name="cancellationToken">A token that cancels the attempt.</param>
    /// <returns><see langword="true"/> if the query succeeded; <see langword="false"/> on a database error.</returns>
    private async Task<bool> CanConnectAsync(string connectionString, string side, CancellationToken cancellationToken)
    {
        await using var connection = databaseProvider.CreateConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var probe = connection.CreateCommand();
            probe.CommandText = "SELECT 1";
            await probe.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch (DbException exception)
        {
            logger.LogWarning(exception, "Health check could not reach the {Side} database.", side);
            return false;
        }
    }
}
