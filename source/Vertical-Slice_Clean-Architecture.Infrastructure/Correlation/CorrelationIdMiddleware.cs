using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Diagnostics;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Correlation;

/// <summary>
/// ASP.NET Core middleware that establishes a correlation ID for every HTTP request.
/// </summary>
/// <remarks>
/// <para>
/// Reuses the caller's <see cref="HeaderName"/> header when it is a well-formed ID (see
/// <see cref="IsValid"/>), otherwise generates a new GUID. The ID is stored in
/// <see cref="CorrelationIdAccessor"/>, echoed back on the response header, added as a
/// <c>correlation.id</c> tag on the current trace activity, and pushed as a <c>CorrelationId</c>
/// logging scope for the rest of the pipeline.
/// </para>
/// <para>
/// Register it (via <c>UseCorrelationIdMiddleware()</c>) first in the pipeline, before
/// <c>UseExceptionHandler()</c>, so error responses and their log entries carry the ID.
/// </para>
/// </remarks>
/// <param name="next">The next middleware in the pipeline.</param>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    /// <summary>
    /// The HTTP header used to receive and return the correlation ID.
    /// </summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>
    /// The longest incoming correlation ID that is accepted.
    /// </summary>
    public const int MaxLength = 64;

    /// <summary>
    /// The characters an incoming correlation ID may contain: letters, digits, <c>-</c>, <c>_</c>, <c>.</c> and <c>:</c>.
    /// </summary>
    private static readonly SearchValues<char> AllowedCharacters =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.:");

    /// <summary>
    /// Resolves the correlation ID for the current request and invokes the rest of the pipeline
    /// inside a logging scope that carries it.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="accessor">The ambient correlation ID store (method-injected per request).</param>
    /// <param name="logger">The logger used to open the correlation ID scope.</param>
    /// <returns>A task that completes when the rest of the pipeline has finished.</returns>
    public async Task InvokeAsync(
        HttpContext context,
        CorrelationIdAccessor accessor,
        ILogger<CorrelationIdMiddleware> logger)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = IsValid(incoming) ? incoming : Guid.NewGuid().ToString();

        accessor.Set(correlationId);
        Activity.Current?.SetTag("correlation.id", correlationId);

        // Written just before the headers are sent rather than now, because UseExceptionHandler
        // clears the response (headers included) before it writes an error, and the error response
        // needs the header most.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope("CorrelationId:{CorrelationId}", correlationId))
        {
            await next(context);
        }
    }

    /// <summary>
    /// Determines whether a caller-supplied correlation ID can be reused. The value is echoed in a
    /// response header and written to logs, so only short values of plain ID characters are
    /// accepted; anything else (including several header values joined by commas) is replaced.
    /// </summary>
    /// <param name="value">The incoming header value.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is 1 to <see cref="MaxLength"/> allowed characters.</returns>
    public static bool IsValid(string? value)
    {
        return !string.IsNullOrEmpty(value)
            && value.Length <= MaxLength
            && !value.AsSpan().ContainsAnyExcept(AllowedCharacters);
    }
}
