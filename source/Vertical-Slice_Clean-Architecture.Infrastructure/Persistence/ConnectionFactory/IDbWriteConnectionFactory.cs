#if (HasDapper)
using System.Data;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.ConnectionFactory;

/// <summary>
/// Creates connections to the write (command) database, for slices that change data.
/// </summary>
public interface IDbWriteConnectionFactory
{
    /// <summary>
    /// Creates a new, unopened connection to the command database. Dapper opens and closes it as
    /// needed; open it yourself when several statements must share a transaction. The caller owns
    /// it and must dispose it.
    /// </summary>
    /// <returns>A new connection.</returns>
    IDbConnection CreateConnection();
}
#endif
