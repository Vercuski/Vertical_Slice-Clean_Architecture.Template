using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vertical_Slice_Clean_Architecture.Domain.Entities;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Contexts;

/// <summary>
/// Entity Framework Core database context for the sample feature slices.
/// </summary>
/// <param name="options">
/// The options for this context (provider, connection string and so on), normally supplied by
/// dependency injection through <c>AddDbContext&lt;SampleDbContext&gt;</c>.
/// </param>
public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets the set of <see cref="SampleEntity"/> records that can be queried and saved.
    /// </summary>
    public DbSet<SampleEntity> Samples => Set<SampleEntity>();

    /// <summary>
    /// Builds the EF Core model by applying every <see cref="IEntityTypeConfiguration{TEntity}"/>
    /// found in the scanned assembly.
    /// </summary>
    /// <param name="modelBuilder">The builder used to construct the model for this context.</param>
    /// <remarks>
    /// The scanned assembly is <c>typeof(DbContext).Assembly</c>, which is the EF Core assembly, not
    /// this Infrastructure assembly. As written, <see cref="SampleEntityConfiguration"/> is therefore
    /// not discovered and its mappings are not applied.
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DbContext).Assembly);
    }
}

/// <summary>
/// Fluent EF Core mapping for <see cref="SampleEntity"/>.
/// </summary>
internal sealed class SampleEntityConfiguration : IEntityTypeConfiguration<SampleEntity>
{
    /// <summary>
    /// Configures the table mapping for <see cref="SampleEntity"/>.
    /// </summary>
    /// <param name="builder">The builder used to configure the entity type.</param>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description><see cref="SampleEntity.Id"/> is the primary key, and the application, not the database, generates it.</description></item>
    ///   <item><description><see cref="SampleEntity.Name"/> is limited to 200 characters.</description></item>
    ///   <item><description><see cref="SampleEntity.Email"/> is limited to 320 characters (the maximum length of an email address) and has a unique index.</description></item>
    /// </list>
    /// </remarks>
    public void Configure(EntityTypeBuilder<SampleEntity> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(200);
        builder.Property(c => c.Email).HasMaxLength(320);
        builder.HasIndex(c => c.Email).IsUnique();
    }
}
