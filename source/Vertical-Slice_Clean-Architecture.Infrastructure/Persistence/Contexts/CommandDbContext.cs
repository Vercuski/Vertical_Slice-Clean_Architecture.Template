#if (HasEfCore)
#if (HasMySql)
using System.Linq.Expressions;
#endif
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
#if (HasMySql)
using Microsoft.EntityFrameworkCore.Metadata;
#endif
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

    // The bulk methods below call DbContextBulkExtensions by name: as extension-method calls on `this`
    // they would bind to these same instance methods and recurse.

    /// <inheritdoc />
    public Task BulkInsertAsync<TEntity>(IEnumerable<TEntity> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        return DbContextBulkExtensions.BulkInsertAsync(this, entities, bulkConfig, progress: null, type: null, cancellationToken);
    }

    /// <inheritdoc />
    public Task BulkUpdateAsync<TEntity>(IEnumerable<TEntity> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        return DbContextBulkExtensions.BulkUpdateAsync(this, entities, bulkConfig, progress: null, type: null, cancellationToken);
    }

    /// <inheritdoc />
    public Task BulkInsertOrUpdateAsync<TEntity>(IEnumerable<TEntity> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        return DbContextBulkExtensions.BulkInsertOrUpdateAsync(this, entities, bulkConfig, progress: null, type: null, cancellationToken);
    }
#if (HasSqlServer)

    /// <inheritdoc />
    public Task BulkInsertOrUpdateOrDeleteAsync<TEntity>(IEnumerable<TEntity> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        return DbContextBulkExtensions.BulkInsertOrUpdateOrDeleteAsync(this, entities, bulkConfig, progress: null, type: null, cancellationToken);
    }
#endif

#if (HasMySql)

    /// <summary>
    /// The most rows <see cref="BulkDeleteAsync{TEntity}"/> deletes with one statement.
    /// </summary>
    private const int DeleteBatchSize = 1000;

    /// <inheritdoc />
    /// <remarks>
    /// On MySQL this deletes by primary key with batched <c>DELETE ... WHERE</c> statements rather than through
    /// EFCore.BulkExtensions. Its MySQL delete copies only the key columns into a temporary table created with
    /// <c>CREATE TABLE ... LIKE</c>, so a unique index on any other column (such as <c>Samples.Email</c>) rejects
    /// all but one of the rows. <paramref name="bulkConfig"/> is not used.
    /// </remarks>
    public async Task BulkDeleteAsync<TEntity>(IEnumerable<TEntity> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entities);
        var keyProperties = (Model.FindEntityType(typeof(TEntity))?.FindPrimaryKey()
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not an entity with a primary key in the model.")).Properties;

        foreach (var batch in entities.Chunk(DeleteBatchSize))
        {
            await Set<TEntity>().Where(MatchesAnyKey(keyProperties, batch)).ExecuteDeleteAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Builds a filter matching the rows whose primary key equals that of any entity in <paramref name="entities"/>:
    /// <c>key IN (...)</c> for a single-column key, otherwise an <c>OR</c> of each entity's key columns.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="keyProperties">The primary key's properties.</param>
    /// <param name="entities">The entities whose rows to match.</param>
    /// <returns>The filter.</returns>
    private static Expression<Func<TEntity, bool>> MatchesAnyKey<TEntity>(IReadOnlyList<IProperty> keyProperties, TEntity[] entities)
        where TEntity : class
    {
        var entity = Expression.Parameter(typeof(TEntity), "entity");

        Expression Column(IProperty property) =>
            Expression.Call(typeof(EF), nameof(EF.Property), [property.ClrType], entity, Expression.Constant(property.Name));

        object? KeyValue(IProperty property, TEntity row) => property.GetGetter().GetClrValue(row);

        if (keyProperties is [var key])
        {
            var values = Array.CreateInstance(key.ClrType, entities.Length);
            for (var index = 0; index < entities.Length; index++)
            {
                values.SetValue(KeyValue(key, entities[index]), index);
            }

            var contains = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [key.ClrType], Expression.Constant(values), Column(key));
            return Expression.Lambda<Func<TEntity, bool>>(contains, entity);
        }

        var anyRow = entities
            .Select(row => keyProperties
                .Select(property => (Expression)Expression.Equal(Column(property), Expression.Constant(KeyValue(property, row), property.ClrType)))
                .Aggregate(Expression.AndAlso))
            .Aggregate(Expression.OrElse);
        return Expression.Lambda<Func<TEntity, bool>>(anyRow, entity);
    }
#else

    /// <inheritdoc />
    public Task BulkDeleteAsync<TEntity>(IEnumerable<TEntity> entities, BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        return DbContextBulkExtensions.BulkDeleteAsync(this, entities, bulkConfig, progress: null, type: null, cancellationToken);
    }
#endif

    /// <inheritdoc />
    public Task BulkSaveChangesAsync(BulkConfig? bulkConfig = null, CancellationToken cancellationToken = default)
    {
        return DbContextBulkExtensions.BulkSaveChangesAsync(this, bulkConfig, progress: null, cancellationToken);
    }

    /// <inheritdoc />
    public Task TruncateAsync<TEntity>(CancellationToken cancellationToken = default)
        where TEntity : class
    {
        return DbContextBulkExtensions.TruncateAsync<TEntity>(this, bulkConfig: (BulkConfig?)null, type: null, cancellationToken);
    }
}
#endif
