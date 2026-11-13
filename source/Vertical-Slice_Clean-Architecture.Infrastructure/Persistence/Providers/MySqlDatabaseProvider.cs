#if (HasMySql)
#if (HasEfCore)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
#endif
using MySqlConnector;
using System.Data.Common;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Providers;

/// <summary>
/// <see cref="IDatabaseProvider"/> for MySQL and MariaDB, built on MySqlConnector.
/// </summary>
#if (HasEfCore)
/// <remarks>
/// EF Core uses the Microting.EntityFrameworkCore.MySql provider (the EF Core 10 continuation of
/// Pomelo), which is itself built on MySqlConnector.
/// </remarks>
/// <param name="serverVersion">
/// The server version EF Core generates SQL for. It is configured explicitly rather than
/// auto-detected, because <c>ServerVersion.AutoDetect</c> opens a database connection just to
/// configure a context.
/// </param>
internal sealed class MySqlDatabaseProvider(ServerVersion serverVersion) : IDatabaseProvider
#else
internal sealed class MySqlDatabaseProvider : IDatabaseProvider
#endif
{
#if (HasEfCore)
    /// <summary>
    /// The configuration key that holds the MySQL or MariaDB server version, for example
    /// <c>8.4.0-mysql</c> or <c>11.4.2-mariadb</c>.
    /// </summary>
    public const string ServerVersionConfigurationKey = "Database:MySqlServerVersion";

#endif
    /// <inheritdoc />
    public string Name => "MySQL";
#if (HasEfCore)

    /// <summary>
    /// Creates the provider, reading the server version from <see cref="ServerVersionConfigurationKey"/>.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The provider.</returns>
    /// <exception cref="InvalidOperationException">The server version is missing or cannot be parsed.</exception>
    public static MySqlDatabaseProvider FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var value = configuration[ServerVersionConfigurationKey];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"'{ServerVersionConfigurationKey}' must be set (for example \"8.4.0-mysql\" or \"11.4.2-mariadb\").");
        }

        if (!ServerVersion.TryParse(value, out var serverVersion))
        {
            throw new InvalidOperationException(
                $"'{ServerVersionConfigurationKey}' value '{value}' is not a valid server version " +
                "(expected for example \"8.4.0-mysql\" or \"11.4.2-mariadb\").");
        }

        return new MySqlDatabaseProvider(serverVersion);
    }

    /// <inheritdoc />
    public void ConfigureEfCore(DbContextOptionsBuilder optionsBuilder, string connectionString)
    {
        optionsBuilder.UseMySql(connectionString, serverVersion);
    }
#endif

    /// <inheritdoc />
    public DbConnection CreateConnection(string connectionString)
    {
        return new MySqlConnection(connectionString);
    }
}
#endif
