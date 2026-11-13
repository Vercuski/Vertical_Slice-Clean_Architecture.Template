#if (HasSqlServer)
using Microsoft.Data.SqlClient;
#if (HasEfCore)
using Microsoft.EntityFrameworkCore;
#endif
using System.Data.Common;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Providers;

/// <summary>
/// <see cref="IDatabaseProvider"/> for Microsoft SQL Server, built on Microsoft.Data.SqlClient.
/// </summary>
internal sealed class SqlServerDatabaseProvider : IDatabaseProvider
{
    /// <inheritdoc />
    public string Name => "SQL Server";
#if (HasEfCore)

    /// <inheritdoc />
    public void ConfigureEfCore(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        optionsBuilder.UseSqlServer(connectionString);
    }
#endif

    /// <inheritdoc />
    public DbConnection CreateConnection(string connectionString)
    {
        return new SqlConnection(connectionString);
    }
}
#endif
