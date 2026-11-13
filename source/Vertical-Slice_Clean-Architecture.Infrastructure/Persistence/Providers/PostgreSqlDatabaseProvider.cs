#if (HasPostgreSql)
#if (HasEfCore)
using Microsoft.EntityFrameworkCore;
#endif
using Npgsql;
using System.Data.Common;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Providers;

/// <summary>
/// <see cref="IDatabaseProvider"/> for PostgreSQL, built on Npgsql.
/// </summary>
/// <remarks>
/// PostgreSQL folds unquoted identifiers to lower case. EF Core quotes every identifier it
/// generates, so a table it creates keeps its exact casing (<c>"Samples"</c>), while hand-written
/// SQL that leaves names unquoted refers to <c>samples</c>. Pick one convention per database.
/// </remarks>
internal sealed class PostgreSqlDatabaseProvider : IDatabaseProvider
{
    /// <inheritdoc />
    public string Name => "PostgreSQL";
#if (HasEfCore)

    /// <inheritdoc />
    public void ConfigureEfCore(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        optionsBuilder.UseNpgsql(connectionString);
    }
#endif

    /// <inheritdoc />
    public DbConnection CreateConnection(string connectionString)
    {
        return new NpgsqlConnection(connectionString);
    }
}
#endif
