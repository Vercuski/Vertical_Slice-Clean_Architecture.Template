using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Vertical_Slice_Clean_Architecture.Infrastructure.Correlation;
using Vertical_Slice_Clean_Architecture.Infrastructure.HealthChecks;
using Vertical_Slice_Clean_Architecture.Infrastructure.Persistence;
using Vertical_Slice_Clean_Architecture.Infrastructure.Versioning;

namespace Vertical_Slice_Clean_Architecture.Infrastructure;

/// <summary>
/// Composition-root extensions that register and wire up the Infrastructure layer's
/// cross-cutting services (health checks, logging, correlation IDs, problem details).
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Maps the health endpoints, both formatted with <see cref="HealthCheckConfiguration.WriteResponse"/>:
    /// <list type="bullet">
    ///   <item><description>
    ///     <c>/health/live</c> runs no checks, so it answers <c>200</c> whenever the process can serve
    ///     requests. Use it for liveness probes: a failing dependency must not get the container restarted.
    ///   </description></item>
    ///   <item><description>
    ///     <c>/health</c> runs every registered check (including the database check) and answers
    ///     <c>200</c> when healthy or degraded and <c>503</c> when unhealthy. Use it for readiness
    ///     probes and monitoring.
    ///   </description></item>
    /// </list>
    /// </summary>
    /// <param name="app">The web application to configure.</param>
    /// <returns>The same <paramref name="app"/>, for chaining.</returns>
    public static WebApplication AddInfrastructureApplicationRegistration(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = HealthCheckConfiguration.WriteResponse
        });
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = HealthCheckConfiguration.WriteResponse
        });
        return app;
    }

    /// <summary>
    /// Adds <see cref="CorrelationIdMiddleware"/> to the request pipeline.
    /// </summary>
    /// <param name="app">The web application to configure.</param>
    /// <returns>The same <paramref name="app"/>, for chaining.</returns>
    /// <remarks>
    /// Call this first in the pipeline, before <c>UseExceptionHandler()</c>, so the correlation ID
    /// is already set when an error response is written and when its log entries are created.
    /// </remarks>
    public static WebApplication UseCorrelationIdMiddleware(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        return app;
    }

    /// <summary>
    /// Registers the Infrastructure layer's services: health checks, logging providers,
    /// the singleton <see cref="CorrelationIdAccessor"/>, the singleton <see cref="ApplicationVersion"/>
    /// (plus a hosted service that logs it at startup), ProblemDetails support, and persistence
    /// (see <c>Persistence/PersistenceRegistration.cs</c>).
    /// </summary>
    /// <param name="builder">The host builder to register services with.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    /// <remarks>
    /// ProblemDetails support is what lets the parameterless <c>UseExceptionHandler()</c> and
    /// <c>UseStatusCodePages()</c> write RFC 9457 bodies. Every body written through it carries a
    /// <c>correlationId</c> member.
    /// </remarks>
    public static IHostApplicationBuilder AddInfrastructureRegistration(this IHostApplicationBuilder builder)
    {
        builder.AddHealthChecksRegistration();
        builder.AddLoggingRegistration();
        builder.Services.AddSingleton<CorrelationIdAccessor>();
        builder.Services.AddSingleton(ApplicationVersion.Current);
        builder.Services.AddHostedService<ApplicationVersionLogger>();
        builder.AddProblemDetailsRegistration();
        builder.AddPersistenceRegistration();
        return builder;
    }

    /// <summary>
    /// How long a single health check may run before it is reported as unhealthy, so an unreachable
    /// dependency cannot make <c>/health</c> hang until the probe itself times out.
    /// </summary>
    private static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Registers every concrete <see cref="IHealthCheck"/> implementation found in this assembly,
    /// named after its type, limited to <see cref="HealthCheckTimeout"/>, and created through
    /// <see cref="ActivatorUtilities"/> from the check's own service scope, so it can take scoped
    /// dependencies such as a DbContext.
    /// </summary>
    /// <param name="builder">The host builder to register services with.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    private static IHostApplicationBuilder AddHealthChecksRegistration(this IHostApplicationBuilder builder)
    {
        var healthCheckBuilder = builder.Services.AddHealthChecks();
        foreach (var healthCheckType in Assembly.GetExecutingAssembly()
            .GetTypes().Where(type => !type.IsAbstract &&
            type.GetInterfaces().Contains(typeof(IHealthCheck))))
        {
            healthCheckBuilder.Add(new HealthCheckRegistration(
                healthCheckType.Name,
                serviceProvider => (IHealthCheck)ActivatorUtilities.CreateInstance(serviceProvider, healthCheckType),
                failureStatus: null,
                tags: null,
                timeout: HealthCheckTimeout));
        }
        return builder;
    }

    /// <summary>
    /// Registers ProblemDetails support and adds the request's correlation ID to every problem
    /// response it writes (unhandled exceptions, validation failures, and empty error status codes),
    /// as a <c>correlationId</c> member next to the framework's <c>traceId</c>.
    /// </summary>
    /// <param name="builder">The host builder to register services with.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    private static IHostApplicationBuilder AddProblemDetailsRegistration(this IHostApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var correlationId = context.HttpContext.RequestServices
                .GetRequiredService<CorrelationIdAccessor>().CorrelationId;
            if (correlationId.Length > 0)
            {
                context.ProblemDetails.Extensions["correlationId"] = correlationId;
            }
        });
        return builder;
    }

    /// <summary>
    /// Replaces the default logging providers with a single console logger, in every environment.
    /// </summary>
    /// <param name="builder">The host builder to register services with.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    /// <remarks>
    /// The console output format comes from the <c>Logging:Console</c> configuration section:
    /// <c>appsettings.json</c> selects the <c>json</c> formatter (one JSON object per entry, for
    /// container log collectors), and <c>appsettings.Development.json</c> selects the readable
    /// <c>simple</c> formatter. Both include scopes, so every entry written during a request carries
    /// its <c>CorrelationId</c>. Add other providers (OpenTelemetry, Application Insights) here.
    /// </remarks>
    private static IHostApplicationBuilder AddLoggingRegistration(this IHostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        return builder;
    }
}
