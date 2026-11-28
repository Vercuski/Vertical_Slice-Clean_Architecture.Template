#if (HasEfCore)
using Microsoft.EntityFrameworkCore;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Contexts;

/// <summary>
/// The EF Core model shared by <see cref="CommandDbContext"/> and <see cref="QueryDbContext"/>, so both
/// sides of the CQRS split map the same schema.
/// </summary>
/// <remarks>
/// <para>
/// Entities enter the model through their <see cref="IEntityTypeConfiguration{TEntity}"/> in the
/// <c>Persistence/Configurations</c> folder, not through <see cref="DbSet{TEntity}"/> properties here:
/// a public <see cref="DbSet{TEntity}"/> can both read and write, which would undo the split.
/// </para>
/// <para>
/// To add an entity, add its configuration; nothing else changes. Slices then reach it with
/// <see cref="IQueryDbContext.Set{TEntity}"/> to read and <see cref="ICommandDbContext.Set{TEntity}"/> to write.
/// </para>
/// </remarks>
/// <typeparam name="TContext">
/// The concrete derived context, so each one receives its own strongly typed
/// <see cref="DbContextOptions{TContext}"/> from dependency injection.
/// </typeparam>
/// <param name="options">The options for the derived context.</param>
internal abstract class BaseDbContext<TContext>(DbContextOptions<TContext> options) : DbContext(options)
    where TContext : DbContext
{
    /// <summary>
    /// Applies every <see cref="IEntityTypeConfiguration{TEntity}"/> defined in the Infrastructure
    /// assembly (see the <c>Persistence/Configurations</c> folder).
    /// </summary>
    /// <param name="modelBuilder">The builder used to construct the model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BaseDbContext<>).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
#endif
