#if (HasEfCore)
using Microsoft.EntityFrameworkCore;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Contexts;

/// <summary>
/// EF Core context for the write side, connected through <c>ConnectionStrings:CommandDbConnection</c>.
/// Slices that change data add, update or remove entities here and call <c>SaveChangesAsync</c>.
/// </summary>
/// <remarks>
/// This context owns the schema, so migrations are created against it:
/// <code>
/// dotnet ef migrations add Initial --context CommandDbContext --output-dir Persistence/Migrations ^
///     --project Vertical-Slice_Clean-Architecture.Infrastructure --startup-project Vertical-Slice_Clean-Architecture.Presentation.API
/// </code>
/// </remarks>
/// <param name="options">The options configured for the command database.</param>
public sealed class CommandDbContext(DbContextOptions<CommandDbContext> options)
    : BaseDbContext<CommandDbContext>(options);
#endif
