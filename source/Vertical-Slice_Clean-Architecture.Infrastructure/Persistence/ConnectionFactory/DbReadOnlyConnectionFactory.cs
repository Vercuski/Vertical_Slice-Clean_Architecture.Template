#if (HasDapper)
using Microsoft.Extensions.Options;
using System.Data;
using Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Options;
using Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Providers;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.ConnectionFactory;

/// <summary>
/// Creates connections to the query database from <see cref="ConnectionStringOptions.QueryDbConnection"/>
/// and the configured <see cref="IDatabaseProvider"/>.
/// </summary>
/// <param name="connectionStringOptions">The bound connection string settings.</param>
/// <param name="databaseProvider">The provider for the configured database engine.</param>
internal sealed class DbReadOnlyConnectionFactory(
    IOptions<ConnectionStringOptions> connectionStringOptions,
    IDatabaseProvider databaseProvider) : IDbReadOnlyConnectionFactory
{
    /// <summary>
    /// The query database connection string, captured at construction.
    /// </summary>
    private readonly string _connectionString = connectionStringOptions.Value.QueryDbConnection;

    /// <inheritdoc />
    public IDbConnection CreateConnection()
    {
        return databaseProvider.CreateConnection(_connectionString);
    }
}
#endif
