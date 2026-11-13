using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Versioning;

/// <summary>
/// Logs the application name, version, and environment once when any host (API, Web, or Console)
/// starts, so every log stream begins by identifying exactly which build produced it.
/// </summary>
/// <param name="logger">The logger used to write the startup entry.</param>
/// <param name="environment">Supplies the application and environment names.</param>
/// <param name="version">The running application's version.</param>
internal sealed class ApplicationVersionLogger(
    ILogger<ApplicationVersionLogger> logger,
    IHostEnvironment environment,
    ApplicationVersion version) : IHostedService
{
    /// <summary>
    /// Writes the startup entry.
    /// </summary>
    /// <param name="cancellationToken">Signaled if startup is aborted.</param>
    /// <returns>A completed task.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Starting {ApplicationName} {Version} ({InformationalVersion}) in {EnvironmentName}",
            environment.ApplicationName,
            version.SemanticVersion,
            version.InformationalVersion,
            environment.EnvironmentName);
        return Task.CompletedTask;
    }

    /// <summary>
    /// No-op; nothing needs to happen at shutdown.
    /// </summary>
    /// <param name="cancellationToken">Signaled if graceful shutdown is aborted.</param>
    /// <returns>A completed task.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
