#if (HasEfCore)
using Microsoft.EntityFrameworkCore;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Contexts;

/// <summary>
/// The <see cref="IWriteSet{TEntity}"/> over a <see cref="DbSet{TEntity}"/>, exposing only its write operations.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <param name="set">The command context's set for <typeparamref name="TEntity"/>.</param>
internal sealed class WriteSet<TEntity>(DbSet<TEntity> set) : IWriteSet<TEntity>
    where TEntity : class
{
    /// <inheritdoc />
    public void Add(TEntity entity) => set.Add(entity);

    /// <inheritdoc />
    public void AddRange(IEnumerable<TEntity> entities) => set.AddRange(entities);

    /// <inheritdoc />
    public void Attach(TEntity entity) => set.Attach(entity);

    /// <inheritdoc />
    public void Update(TEntity entity) => set.Update(entity);

    /// <inheritdoc />
    public void Remove(TEntity entity) => set.Remove(entity);

    /// <inheritdoc />
    public void RemoveRange(IEnumerable<TEntity> entities) => set.RemoveRange(entities);
}
#endif
