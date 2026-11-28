#if (HasEfCore)
using EFCore.BulkExtensions;
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
/// <para>
/// For large batches, the bulk methods (<see cref="BulkInsertAsync{TEntity}"/>, <see cref="BulkUpdateAsync{TEntity}"/>,
/// <see cref="BulkDeleteAsync{TEntity}"/> and the rest, from EFCore.BulkExtensions) write straight to the database in
/// one round trip per batch. They bypass the change tracker: they do not see changes staged through
/// <see cref="Set{TEntity}"/>, and they do not wait for <see cref="SaveChangesAsync"/>. Each call commits on
/// its own unless it runs inside a transaction from <see cref="BeginTransactionAsync"/>.
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

    /// <summary>
    /// Inserts every entity in <paramref name="entities"/> in one bulk operation.
    /// </summary>
    /// <typeparam name="TEntity">An entity in the model.</typeparam>
    /// <param name="entities">The new entities.</param>
    /// <param name="bulkConfig">
    /// Optional settings, such as <see cref="BulkConfig.BatchSize"/> or <see cref="BulkConfig.SetOutputIdentity"/>
    /// to copy database-generated keys back onto <paramref name="entities"/>.
    /// </param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the rows are written.</returns>
    Task BulkInsertAsync<TEntity>(IEnumerable<TEntity> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default)
        where TEntity : class;

    /// <summary>
    /// Updates every entity in <paramref name="entities"/> in one bulk operation, matching rows by primary key.
    /// </summary>
    /// <typeparam name="TEntity">An entity in the model.</typeparam>
    /// <param name="entities">Entities that already exist in the database, with their keys set.</param>
    /// <param name="bulkConfig">
    /// Optional settings, such as <see cref="BulkConfig.UpdateByProperties"/> to match on other columns, or
    /// <see cref="BulkConfig.PropertiesToInclude"/> to write only some columns.
    /// </param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the rows are written.</returns>
    Task BulkUpdateAsync<TEntity>(IEnumerable<TEntity> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default)
        where TEntity : class;

    /// <summary>
    /// Inserts the entities in <paramref name="entities"/> that do not exist yet and updates the ones that
    /// do (an upsert), in one bulk operation, matching rows by primary key.
    /// </summary>
    /// <typeparam name="TEntity">An entity in the model.</typeparam>
    /// <param name="entities">The entities to insert or update.</param>
    /// <param name="bulkConfig">Optional settings, such as <see cref="BulkConfig.UpdateByProperties"/> to match on other columns.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the rows are written.</returns>
    Task BulkInsertOrUpdateAsync<TEntity>(IEnumerable<TEntity> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default)
        where TEntity : class;
#if (HasSqlServer)

    /// <summary>
    /// Makes the <typeparamref name="TEntity"/> table match <paramref name="entities"/> in one bulk operation:
    /// inserts the new ones, updates the existing ones, and <b>deletes every other row in the table</b>.
    /// </summary>
    /// <remarks>
    /// Passing an empty sequence deletes every row. Use <see cref="BulkConfig"/>'s synchronize filter to limit
    /// the delete to a subset of rows. Available on SQL Server only.
    /// </remarks>
    /// <typeparam name="TEntity">An entity in the model.</typeparam>
    /// <param name="entities">The complete set of rows the table should hold.</param>
    /// <param name="bulkConfig">Optional settings, such as <see cref="BulkConfig.UpdateByProperties"/> to match on other columns.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the rows are written.</returns>
    Task BulkInsertOrUpdateOrDeleteAsync<TEntity>(IEnumerable<TEntity> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default)
        where TEntity : class;
#endif

    /// <summary>
    /// Deletes every entity in <paramref name="entities"/> in one bulk operation, matching rows by primary key.
    /// </summary>
#if (HasMySql)
    /// <remarks>
    /// On MySQL rows are always matched by primary key, in batched <c>DELETE</c> statements, and
    /// <paramref name="bulkConfig"/> is not used.
    /// </remarks>
#endif
    /// <typeparam name="TEntity">An entity in the model.</typeparam>
    /// <param name="entities">Entities that exist in the database, with their keys set.</param>
    /// <param name="bulkConfig">Optional settings, such as <see cref="BulkConfig.UpdateByProperties"/> to match on other columns.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the rows are deleted.</returns>
    Task BulkDeleteAsync<TEntity>(IEnumerable<TEntity> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default)
        where TEntity : class;

    /// <summary>
    /// Saves every pending change staged through <see cref="Set{TEntity}"/> as bulk operations instead of
    /// row-by-row statements. Use it in place of <see cref="SaveChangesAsync"/> when thousands of changes are staged.
    /// </summary>
    /// <param name="bulkConfig">Optional settings, such as <see cref="BulkConfig.BatchSize"/>.</param>
    /// <param name="cancellationToken">A token that cancels the save.</param>
    /// <returns>A task that completes when the changes are written.</returns>
    Task BulkSaveChangesAsync(BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every row from the table <typeparamref name="TEntity"/> is mapped to, with <c>TRUNCATE TABLE</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Faster than deleting the rows, but database errors are not caught: truncating a table that another
    /// table's foreign key references fails on SQL Server and PostgreSQL, and the caller sees that exception.
    /// </para>
    /// <para>
    /// On MySQL, <c>TRUNCATE</c> commits implicitly, so it cannot be rolled back even inside a transaction
    /// from <see cref="BeginTransactionAsync"/>. On SQL Server and PostgreSQL it can.
    /// </para>
    /// </remarks>
    /// <typeparam name="TEntity">
    /// An entity in the model. Any other type throws <see cref="InvalidOperationException"/>.
    /// </typeparam>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the table is empty.</returns>
    Task TruncateAsync<TEntity>(CancellationToken cancellationToken = default)
        where TEntity : class;
}
#endif
