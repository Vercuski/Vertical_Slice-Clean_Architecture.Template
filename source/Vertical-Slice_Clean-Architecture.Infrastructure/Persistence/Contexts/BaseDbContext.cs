#if (HasEfCore)
using Microsoft.EntityFrameworkCore;
using Vertical_Slice_Clean_Architecture.Domain.Entities;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Contexts;

/// <summary>
/// The EF Core model shared by <see cref="CommandDbContext"/> and <see cref="QueryDbContext"/>.
/// Declare entity sets here so both sides of the CQRS split map the same schema.
/// </summary>
/// <typeparam name="TContext">
/// The concrete derived context, so each one receives its own strongly typed
/// <see cref="DbContextOptions{TContext}"/> from dependency injection.
/// </typeparam>
/// <param name="options">The options for the derived context.</param>
public abstract class BaseDbContext<TContext>(DbContextOptions<TContext> options) : DbContext(options)
    where TContext : DbContext
{
    /// <summary>
    /// Gets the set of <see cref="SampleEntity"/> rows.
    /// </summary>
    public DbSet<SampleEntity> Samples => Set<SampleEntity>();

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
