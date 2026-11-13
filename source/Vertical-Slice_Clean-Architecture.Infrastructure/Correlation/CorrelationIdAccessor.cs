namespace Vertical_Slice_Clean_Architecture.Infrastructure.Correlation;

/// <summary>
/// Gives access to the current operation's correlation ID. The value is held in an
/// <see cref="AsyncLocal{T}"/>, so it flows through the whole async call chain of a request (every
/// await, handler and data-access call) without being passed as a parameter.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a singleton by <c>AddInfrastructureRegistration()</c>; inject it wherever the ID is
/// needed (for example to forward it on outgoing HTTP calls). That is safe because the backing
/// <see cref="AsyncLocal{T}"/> is static and keeps a separate value per async flow, so concurrent
/// requests never see each other's IDs.
/// </para>
/// <para>
/// <see cref="CorrelationIdMiddleware"/> sets the value at the start of every HTTP request.
/// </para>
/// </remarks>
public sealed class CorrelationIdAccessor
{
    /// <summary>
    /// Holds the correlation ID for the current async flow.
    /// </summary>
    private static readonly AsyncLocal<string?> Current = new();

    /// <summary>
    /// Gets the correlation ID for the current async flow, or <see cref="string.Empty"/> outside a
    /// request (for example during startup or in a background service).
    /// </summary>
    public string CorrelationId => Current.Value ?? string.Empty;

    /// <summary>
    /// Sets the correlation ID for the current async flow and every flow it subsequently starts.
    /// </summary>
    /// <param name="correlationId">The correlation ID to make ambient.</param>
    /// <exception cref="ArgumentException"><paramref name="correlationId"/> is null, empty, or whitespace.</exception>
    public void Set(string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        Current.Value = correlationId;
    }
}
