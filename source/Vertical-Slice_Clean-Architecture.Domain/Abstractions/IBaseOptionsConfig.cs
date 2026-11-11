namespace Vertical_Slice_Clean_Architecture.Domain.Abstractions;

/// <summary>
/// Contract for strongly typed options classes that are bound from application configuration.
/// </summary>
/// <remarks>
/// Implementations name the configuration section they bind to, so that registration code can
/// bind any options type generically (for example
/// <c>services.Configure&lt;TOptions&gt;(configuration.GetSection(options.Section))</c>)
/// without hard-coding section names.
/// </remarks>
public interface IBaseOptionsConfig
{
    /// <summary>
    /// Gets the name of the configuration section (for example in <c>appsettings.json</c>)
    /// that this options type is bound from.
    /// </summary>
    public abstract string Section { get; }
}
