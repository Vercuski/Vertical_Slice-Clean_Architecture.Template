#if (HasDapper)
using System.Data;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.ConnectionFactory;

/// <summary>
/// Creates connections to the read (query) database, for slices that only read data.
/// </summary>
public interface IDbReadOnlyConnectionFactory
{
    /// <summary>
    /// Creates a new, unopened connection to the query database. Dapper opens and closes it as
    /// needed; the caller owns it and must dispose it.
    /// </summary>
    /// <returns>A new connection.</returns>
    IDbConnection CreateConnection();
}
#endif
