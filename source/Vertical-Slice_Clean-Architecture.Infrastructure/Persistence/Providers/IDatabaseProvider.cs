#if (HasEfCore)
using Microsoft.EntityFrameworkCore;
#endif
using System.Data.Common;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Providers;

/// <summary>
/// Isolates everything that is specific to the database engine, so the rest of the persistence
/// code (contexts, connection factories, registration) is engine-agnostic.
/// </summary>
/// <remarks>
/// Exactly one implementation is created at startup by <c>PersistenceRegistration</c>.
/// To move to a different engine, add an implementation for it, reference its ADO.NET driver
/// (and EF Core provider, if used), and return it from <c>CreateDatabaseProvider</c>.
/// </remarks>
public interface IDatabaseProvider
{
    /// <summary>
    /// Gets a short, human-readable name for the engine (for example <c>SQL Server</c>), used in
    /// startup and diagnostic messages.
    /// </summary>
    string Name { get; }
#if (HasEfCore)

    /// <summary>
    /// Configures <paramref name="optionsBuilder"/> to use this engine's EF Core provider.
    /// </summary>
    /// <param name="optionsBuilder">The EF Core options builder to configure.</param>
    /// <param name="connectionString">The connection string to use.</param>
    void ConfigureEfCore(DbContextOptionsBuilder optionsBuilder, string connectionString);
#endif

    /// <summary>
    /// Creates a new, unopened ADO.NET connection for this engine.
    /// </summary>
    /// <remarks>
    /// Used by the Dapper connection factories and by the database health check, which opens its
    /// own connections so it never shares one with a request's DbContext.
    /// </remarks>
    /// <param name="connectionString">The connection string to use.</param>
    /// <returns>A new connection. The caller owns it and must dispose it.</returns>
    DbConnection CreateConnection(string connectionString);
}
