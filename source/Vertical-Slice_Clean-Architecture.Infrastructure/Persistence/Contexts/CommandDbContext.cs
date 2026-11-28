#if (HasEfCore)
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Contexts;

/// <summary>
/// EF Core context for the write side, connected through <c>ConnectionStrings:CommandDbConnection</c>.
/// Slices use it through <see cref="ICommandDbContext"/>, which can change data but not query it.
/// </summary>
/// <remarks>
/// It is internal so slices cannot bypass <see cref="ICommandDbContext"/> and use the full
/// <see cref="DbContext"/> surface. It owns the schema, so migrations are created against it:
/// <code>
/// dotnet ef migrations add Initial --context CommandDbContext --output-dir Persistence/Migrations ^
///     --project Vertical-Slice_Clean-Architecture.Infrastructure --startup-project Vertical-Slice_Clean-Architecture.Presentation.API
/// </code>
/// </remarks>
/// <param name="options">The options configured for the command database.</param>
internal sealed class CommandDbContext(DbContextOptions<CommandDbContext> options)
    : BaseDbContext<CommandDbContext>(options), ICommandDbContext
{
    /// <inheritdoc />
    /// <remarks>
    /// Implemented explicitly so it does not hide <see cref="DbContext.Set{TEntity}()"/>, which the
    /// write set wraps.
    /// </remarks>
    IWriteSet<TEntity> ICommandDbContext.Set<TEntity>()
    {
        return new WriteSet<TEntity>(Set<TEntity>());
    }

    /// <inheritdoc />
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Database.BeginTransactionAsync(cancellationToken);
    }
}
#endif
