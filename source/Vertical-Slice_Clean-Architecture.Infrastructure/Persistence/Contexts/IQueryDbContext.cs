#if (HasEfCore)
namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Contexts;

/// <summary>
/// The read side of the database, connected through <c>ConnectionStrings:QueryDbConnection</c>.
/// Slices that read data depend on this interface.
/// </summary>
/// <remarks>
/// It can only query: <see cref="Set{TEntity}"/> returns an <see cref="IQueryable{T}"/> for any entity
/// in the model, so there is no way to add, update or remove entities or to save changes through it.
/// Entities read here are not tracked. Use <see cref="ICommandDbContext"/> to change data.
/// </remarks>
public interface IQueryDbContext
{
    /// <summary>
    /// Gets a no-tracking query over the rows of <typeparamref name="TEntity"/>.
    /// </summary>
    /// <typeparam name="TEntity">
    /// An entity in the model, that is, one with an <c>IEntityTypeConfiguration</c> in
    /// <c>Persistence/Configurations</c>. Any other type throws <see cref="InvalidOperationException"/>.
    /// </typeparam>
    /// <returns>The query.</returns>
    IQueryable<TEntity> Set<TEntity>()
        where TEntity : class;
}
#endif
