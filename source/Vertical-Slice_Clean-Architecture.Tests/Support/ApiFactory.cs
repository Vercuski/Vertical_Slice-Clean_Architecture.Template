using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Vertical_Slice_Clean_Architecture.Tests.Support;

/// <summary>
/// Hosts the real API (its <c>Program.cs</c>, middleware and every slice) in memory, pointed at the
/// given database.
/// </summary>
/// <remarks>
/// The host runs in the Development environment, where the service provider validates every
/// registration when it is built. A missing registration (for example a handler whose DbContext was
/// never added) therefore fails the tests at startup, as it would fail the real application.
/// </remarks>
/// <param name="connectionString">Used for both the query and the command connection strings.</param>
internal sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    /// <summary>
    /// Selects the Development environment and replaces both connection strings.
    /// </summary>
    /// <param name="builder">The web host builder to configure.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:QueryDbConnection", connectionString);
        builder.UseSetting("ConnectionStrings:CommandDbConnection", connectionString);
    }
}
