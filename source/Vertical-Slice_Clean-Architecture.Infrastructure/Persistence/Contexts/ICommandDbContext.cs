#if (HasEfCore)
using Microsoft.EntityFrameworkCore.Storage;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Contexts;

/// <summary>
/// The write side of the database, connected through <c>ConnectionStrings:CommandDbConnection</c>.
/// Slices that change data depend on this interface.
/// </summary>
/// <remarks>
/// <para>
/// It can do everything except query: <see cref="Set{TEntity}"/> returns an <see cref="IWriteSet{TEntity}"/>
/// for any entity in the model, which adds, attaches, updates and removes entities but cannot read them,
/// and changes are committed with <see cref="SaveChangesAsync"/>, optionally inside a transaction from
/// <see cref="BeginTransactionAsync"/>. Changes to several entity types are saved together.
/// </para>
/// <para>
/// Reads a command needs, such as checking that a value is not already in use, go through
/// <see cref="IQueryDbContext"/>. When the query side is a replica that read can lag behind the
/// command side, so enforce rules that must hold (uniqueness, for example) with database constraints too.
/// </para>
/// </remarks>
public interface ICommandDbContext
{
    /// <summary>
    /// Gets the write-only set of <typeparamref name="TEntity"/> rows.
    /// </summary>
    /// <typeparam name="TEntity">
    /// An entity in the model, that is, one with an <c>IEntityTypeConfiguration</c> in
    /// <c>Persistence/Configurations</c>. Any other type throws <see cref="InvalidOperationException"/>
    /// when the set is used.
    /// </typeparam>
    /// <returns>The write set.</returns>
    IWriteSet<TEntity> Set<TEntity>()
        where TEntity : class;

    /// <summary>
    /// Saves every pending add, update and remove, across all entity types, to the command database.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the save.</param>
    /// <returns>The number of rows written.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a transaction on the command database, for work that spans several saves.
    /// A single <see cref="SaveChangesAsync"/> is already transactional.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels starting the transaction.</param>
    /// <returns>The transaction; commit it, or dispose it to roll back.</returns>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
#endif
