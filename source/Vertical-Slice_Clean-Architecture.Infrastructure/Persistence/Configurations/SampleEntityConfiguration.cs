#if (HasEfCore)
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vertical_Slice_Clean_Architecture.Domain.Entities;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent EF Core mapping for <see cref="SampleEntity"/>, applied to both contexts by
/// <c>BaseDbContext.OnModelCreating</c>.
/// </summary>
internal sealed class SampleEntityConfiguration : IEntityTypeConfiguration<SampleEntity>
{
    /// <summary>
    /// Configures the <c>Samples</c> table.
    /// </summary>
    /// <param name="builder">The builder used to configure the entity type.</param>
    /// <remarks>
    /// <list type="bullet">
    ///   <item><description><see cref="SampleEntity.Id"/> is the primary key, and the application, not the database, generates it.</description></item>
    ///   <item><description><see cref="SampleEntity.Name"/> is limited to <see cref="SampleEntity.NameMaxLength"/> characters.</description></item>
    ///   <item><description><see cref="SampleEntity.Email"/> is limited to <see cref="SampleEntity.EmailMaxLength"/> characters and has a unique index.</description></item>
    /// </list>
    /// </remarks>
    public void Configure(EntityTypeBuilder<SampleEntity> builder)
    {
        builder.ToTable("Samples");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Name).HasMaxLength(SampleEntity.NameMaxLength);
        builder.Property(s => s.Email).HasMaxLength(SampleEntity.EmailMaxLength);
        builder.HasIndex(s => s.Email).IsUnique();
    }
}
#endif
