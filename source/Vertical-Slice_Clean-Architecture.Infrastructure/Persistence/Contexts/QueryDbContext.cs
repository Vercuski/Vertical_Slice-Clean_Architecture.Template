#if (HasEfCore)
using Microsoft.EntityFrameworkCore;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Contexts;

/// <summary>
/// EF Core context for the read side, connected through <c>ConnectionStrings:QueryDbConnection</c>.
/// </summary>
/// <remarks>
/// Queries default to <see cref="QueryTrackingBehavior.NoTracking"/>, because entities read here are
/// never saved back through this context. Use <see cref="CommandDbContext"/> to change data.
/// </remarks>
/// <param name="options">The options configured for the query database.</param>
public sealed class QueryDbContext(DbContextOptions<QueryDbContext> options)
    : BaseDbContext<QueryDbContext>(options)
{
    /// <summary>
    /// Sets <see cref="QueryTrackingBehavior.NoTracking"/> as this context's default.
    /// </summary>
    /// <param name="optionsBuilder">The builder used to configure the context.</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        base.OnConfiguring(optionsBuilder);
    }
}
#endif
