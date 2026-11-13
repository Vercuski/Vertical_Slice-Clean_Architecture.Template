using System.ComponentModel.DataAnnotations;
using Vertical_Slice_Clean_Architecture.Domain.Abstractions;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Options;

/// <summary>
/// Connection strings for the read (query) and write (command) sides of the CQRS split, bound from
/// the <c>ConnectionStrings</c> configuration section.
/// </summary>
/// <remarks>
/// Both values are required and are validated when the host starts, so a missing connection string
/// fails immediately rather than on the first database call. The two sides may point at the same
/// database; split them when reads should go to a replica.
/// </remarks>
public sealed record ConnectionStringOptions : IBaseOptionsConfig
{
    /// <summary>
    /// Gets or sets the connection string for the read (query) database.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string QueryDbConnection { get; set; } = null!;

    /// <summary>
    /// Gets or sets the connection string for the write (command) database.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string CommandDbConnection { get; set; } = null!;

    /// <inheritdoc />
    public string Section => "ConnectionStrings";
}
