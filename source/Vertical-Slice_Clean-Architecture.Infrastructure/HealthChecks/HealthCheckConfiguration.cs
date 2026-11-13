using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text;
using System.Text.Json;
using Vertical_Slice_Clean_Architecture.Infrastructure.Versioning;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.HealthChecks;

/// <summary>
/// Formats health check results for the <c>/health</c> endpoint.
/// </summary>
public class HealthCheckConfiguration
{
    /// <summary>
    /// Prevents instantiation; this type only exposes static members.
    /// </summary>
    protected HealthCheckConfiguration() { }

    /// <summary>
    /// Writes <paramref name="healthReport"/> to the response as indented JSON containing the
    /// overall status, the application version (<see cref="ApplicationVersion"/>), and, for each
    /// registered check, its status, description, and data.
    /// </summary>
    /// <param name="context">The HTTP context of the health check request.</param>
    /// <param name="healthReport">The aggregated health check results.</param>
    /// <returns>A task that completes when the response body has been written.</returns>
    /// <example>
    /// <code>
    /// {
    ///   "status": "Healthy",
    ///   "version": "1.4.1-alpha.0.3",
    ///   "informationalVersion": "1.4.1-alpha.0.3+2057147a6910abdd18c2360adb13dea4c2cb3df6",
    ///   "results": {
    ///     "DatabaseHealthCheck": {
    ///       "status": "Healthy",
    ///       "description": "Both databases are reachable.",
    ///       "data": { "engine": "PostgreSQL", "commandDatabase": "Reachable", "queryDatabase": "Reachable" }
    ///     }
    ///   }
    /// }
    /// </code>
    /// </example>
    public static Task WriteResponse(HttpContext context, HealthReport healthReport)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var options = new JsonWriterOptions { Indented = true };

        using var memoryStream = new MemoryStream();
        using (var jsonWriter = new Utf8JsonWriter(memoryStream, options))
        {
            jsonWriter.WriteStartObject();
            jsonWriter.WriteString("status", healthReport.Status.ToString());
            jsonWriter.WriteString("version", ApplicationVersion.Current.SemanticVersion);
            jsonWriter.WriteString("informationalVersion", ApplicationVersion.Current.InformationalVersion);
            jsonWriter.WriteStartObject("results");

            foreach (var healthReportEntry in healthReport.Entries)
            {
                jsonWriter.WriteStartObject(healthReportEntry.Key);
                jsonWriter.WriteString("status",
                    healthReportEntry.Value.Status.ToString());
                jsonWriter.WriteString("description",
                    healthReportEntry.Value.Description);
                jsonWriter.WriteStartObject("data");

                foreach (var item in healthReportEntry.Value.Data)
                {
                    jsonWriter.WritePropertyName(item.Key);

                    JsonSerializer.Serialize(jsonWriter, item.Value,
                        item.Value?.GetType() ?? typeof(object));
                }

                jsonWriter.WriteEndObject();
                jsonWriter.WriteEndObject();
            }

            jsonWriter.WriteEndObject();
            jsonWriter.WriteEndObject();
        }

        return context.Response.WriteAsync(
            Encoding.UTF8.GetString(memoryStream.ToArray()));
    }
}
