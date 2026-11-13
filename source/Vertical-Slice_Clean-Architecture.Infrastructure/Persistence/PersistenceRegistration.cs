#if (HasEfCore)
using Microsoft.EntityFrameworkCore;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
#if (HasEfCore)
using Microsoft.Extensions.Options;
#endif
#if (HasDapper)
using Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.ConnectionFactory;
#endif
#if (HasEfCore)
using Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Contexts;
#endif
using Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Options;
using Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Providers;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Persistence;

/// <summary>
/// Registers the persistence services: the connection strings, the database engine's
/// <see cref="IDatabaseProvider"/>, and the data-access services the feature slices depend on.
/// </summary>
/// <remarks>
/// Called by <c>AddInfrastructureRegistration()</c>. The slices resolve:
/// <list type="bullet">
#if (HasEfCore)
///   <item><description><see cref="QueryDbContext"/> for reads (no tracking) and <see cref="CommandDbContext"/> for writes.</description></item>
#endif
#if (HasDapper)
///   <item><description><see cref="IDbReadOnlyConnectionFactory"/> for reads and <see cref="IDbWriteConnectionFactory"/> for writes.</description></item>
#endif
/// </list>
/// </remarks>
internal static class PersistenceRegistration
{
    /// <summary>
    /// Registers all persistence services.
    /// </summary>
    /// <param name="builder">The host builder to register services with.</param>
    /// <returns>The same <paramref name="builder"/>, for chaining.</returns>
    /// <remarks>
    /// <see cref="ConnectionStringOptions"/> is validated when the host starts, so a missing
    /// connection string stops the application immediately with a message naming the setting.
    /// </remarks>
    internal static IHostApplicationBuilder AddPersistenceRegistration(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<ConnectionStringOptions>()
            .Bind(builder.Configuration.GetSection(new ConnectionStringOptions().Section))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // The engine this solution was generated for.
#if (HasPostgreSql)
        IDatabaseProvider databaseProvider = new PostgreSqlDatabaseProvider();
#elif (HasMySql && HasEfCore)
        IDatabaseProvider databaseProvider = MySqlDatabaseProvider.FromConfiguration(builder.Configuration);
#elif (HasMySql)
        IDatabaseProvider databaseProvider = new MySqlDatabaseProvider();
#else
        IDatabaseProvider databaseProvider = new SqlServerDatabaseProvider();
#endif
        builder.Services.AddSingleton(databaseProvider);
#if (HasEfCore)

        builder.Services.AddDbContext<CommandDbContext>((serviceProvider, options) =>
            ConfigureDbContext(builder, serviceProvider, options, databaseProvider, connectionStrings => connectionStrings.CommandDbConnection));
        builder.Services.AddDbContext<QueryDbContext>((serviceProvider, options) =>
            ConfigureDbContext(builder, serviceProvider, options, databaseProvider, connectionStrings => connectionStrings.QueryDbConnection));
#endif
#if (HasDapper)

        builder.Services.AddSingleton<IDbReadOnlyConnectionFactory, DbReadOnlyConnectionFactory>();
        builder.Services.AddSingleton<IDbWriteConnectionFactory, DbWriteConnectionFactory>();
#endif

        return builder;
    }
#if (HasEfCore)

    /// <summary>
    /// Points a context at its connection string through <paramref name="databaseProvider"/>, and
    /// turns on detailed errors and sensitive data logging in the Development environment only.
    /// </summary>
    /// <param name="builder">The host builder, used to read the environment.</param>
    /// <param name="serviceProvider">The scoped service provider creating the context.</param>
    /// <param name="options">The options builder for the context.</param>
    /// <param name="databaseProvider">The provider for the configured database engine.</param>
    /// <param name="selectConnectionString">Picks this context's connection string.</param>
    private static void ConfigureDbContext(
        IHostApplicationBuilder builder,
        IServiceProvider serviceProvider,
        DbContextOptionsBuilder options,
        IDatabaseProvider databaseProvider,
        Func<ConnectionStringOptions, string> selectConnectionString)
    {
        var connectionStrings = serviceProvider.GetRequiredService<IOptions<ConnectionStringOptions>>().Value;
        databaseProvider.ConfigureEfCore(options, selectConnectionString(connectionStrings));

        if (builder.Environment.IsDevelopment())
        {
            options.EnableDetailedErrors().EnableSensitiveDataLogging();
        }
    }
#endif
}
