#if (HasEfCore)
using Microsoft.EntityFrameworkCore;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Contexts;

/// <summary>
/// EF Core context for the read side, connected through <c>ConnectionStrings:QueryDbConnection</c>.
/// Slices use it through <see cref="IQueryDbContext"/>, which can only query.
/// </summary>
/// <remarks>
/// It is internal so slices cannot bypass <see cref="IQueryDbContext"/> and use the full
/// <see cref="DbContext"/> surface. Queries default to <see cref="QueryTrackingBehavior.NoTracking"/>,
/// and saving changes throws, so even code inside Infrastructure cannot write through it.
/// Use <see cref="ICommandDbContext"/> to change data.
/// </remarks>
/// <param name="options">The options configured for the query database.</param>
internal sealed class QueryDbContext(DbContextOptions<QueryDbContext> options)
    : BaseDbContext<QueryDbContext>(options), IQueryDbContext
{
    /// <summary>
    /// The message of the exception thrown when something tries to save through this context.
    /// </summary>
    internal const string ReadOnlyMessage =
        "QueryDbContext is read-only. Change data through ICommandDbContext.";

    /// <inheritdoc />
    /// <remarks>
    /// Implemented explicitly so it does not hide <see cref="DbContext.Set{TEntity}()"/>. Returning the
    /// set as <see cref="IQueryable{T}"/> leaves nothing that can write; tracking is off by default
    /// (see <see cref="OnConfiguring"/>).
    /// </remarks>
    IQueryable<TEntity> IQueryDbContext.Set<TEntity>()
    {
        return Set<TEntity>();
    }

    /// <summary>
    /// Always throws: the query context cannot write.
    /// </summary>
    /// <param name="acceptAllChangesOnSuccess">Not used.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="InvalidOperationException">Always.</exception>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        throw new InvalidOperationException(ReadOnlyMessage);
    }

    /// <summary>
    /// Always throws: the query context cannot write.
    /// </summary>
    /// <param name="acceptAllChangesOnSuccess">Not used.</param>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>Never returns.</returns>
    /// <exception cref="InvalidOperationException">Always.</exception>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(ReadOnlyMessage);
    }

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
