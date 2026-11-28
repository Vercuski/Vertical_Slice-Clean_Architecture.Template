#if (HasEfCore)
namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Contexts;

/// <summary>
/// Stages changes to one entity type on <see cref="ICommandDbContext"/>. It cannot read: there is
/// no query, enumeration or lookup by key. Changes are written by <see cref="ICommandDbContext.SaveChangesAsync"/>.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IWriteSet<in TEntity>
    where TEntity : class
{
    /// <summary>
    /// Marks <paramref name="entity"/> to be inserted.
    /// </summary>
    /// <param name="entity">The new entity.</param>
    void Add(TEntity entity);

    /// <summary>
    /// Marks every entity in <paramref name="entities"/> to be inserted.
    /// </summary>
    /// <param name="entities">The new entities.</param>
    void AddRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Starts tracking an existing <paramref name="entity"/> as unchanged, so only the properties
    /// changed afterwards are written.
    /// </summary>
    /// <param name="entity">An entity that already exists in the database, with its key set.</param>
    void Attach(TEntity entity);

    /// <summary>
    /// Marks an existing <paramref name="entity"/> to be updated; every property is written.
    /// </summary>
    /// <param name="entity">An entity that already exists in the database, with its key set.</param>
    void Update(TEntity entity);

    /// <summary>
    /// Marks an existing <paramref name="entity"/> to be deleted.
    /// </summary>
    /// <param name="entity">An entity that already exists in the database, with its key set.</param>
    void Remove(TEntity entity);

    /// <summary>
    /// Marks every entity in <paramref name="entities"/> to be deleted.
    /// </summary>
    /// <param name="entities">Entities that already exist in the database, with their keys set.</param>
    void RemoveRange(IEnumerable<TEntity> entities);
}
#endif
