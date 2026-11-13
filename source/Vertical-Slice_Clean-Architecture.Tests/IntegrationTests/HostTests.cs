using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Vertical_Slice_Clean_Architecture.Infrastructure.Correlation;
using Vertical_Slice_Clean_Architecture.Tests.Support;

namespace Vertical_Slice_Clean_Architecture.Tests.IntegrationTests;

/// <summary>
/// Tests the real API host (startup, middleware order, health endpoints, error responses) with
/// connection strings that point nowhere, so they need neither Docker nor a database.
/// </summary>
/// <remarks>
/// Starting the host at all is the first assertion: in the Development environment every service
/// registration is validated when the host is built, so a slice whose dependencies are not
/// registered fails here.
/// </remarks>
[TestFixture]
public class HostTests
{
    /// <summary>
    /// The in-memory host shared by the tests in this fixture.
    /// </summary>
    private ApiFactory _factory = null!;

    /// <summary>
    /// A client for <see cref="_factory"/>.
    /// </summary>
    private HttpClient _client = null!;

    /// <summary>
    /// Starts the host.
    /// </summary>
    [OneTimeSetUp]
    public void StartHost()
    {
        _factory = new ApiFactory(TestDatabase.UnreachableConnectionString);
        _client = _factory.CreateClient();
    }

    /// <summary>
    /// Stops the host.
    /// </summary>
    [OneTimeTearDown]
    public void StopHost()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>
    /// <c>/health/live</c> answers 200 even though the database is unreachable, and reports the version.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Live_Should_BeHealthy_WithoutTheDatabase()
    {
        var response = await _client.GetAsync("/health/live");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.GetProperty("status").GetString(), Is.EqualTo("Healthy"));
            Assert.That(body.GetProperty("version").GetString(), Is.Not.Empty);
        }
    }

    /// <summary>
    /// <c>/health</c> runs the database check, so an unreachable database makes it 503, naming both sides.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Health_Should_BeUnhealthy_WhenTheDatabaseIsUnreachable()
    {
        var response = await _client.GetAsync("/health");
        var check = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("results").GetProperty("DatabaseHealthCheck");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(check.GetProperty("status").GetString(), Is.EqualTo("Unhealthy"));
            Assert.That(check.GetProperty("data").GetProperty("commandDatabase").GetString(), Is.EqualTo("Unreachable"));
            Assert.That(check.GetProperty("data").GetProperty("queryDatabase").GetString(), Is.EqualTo("Unreachable"));
        }
    }

    /// <summary>
    /// Every response carries a correlation ID; without an incoming one, a GUID is generated.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Response_Should_CarryAGeneratedCorrelationId()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.That(Guid.TryParse(CorrelationIdOf(response), out _), Is.True);
    }

    /// <summary>
    /// A valid incoming correlation ID is echoed; an unsafe one is replaced.
    /// </summary>
    /// <param name="incoming">The incoming header value.</param>
    /// <param name="echoed">Whether the value is expected back unchanged.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [TestCase("order-123:abc", true)]
    [TestCase("<script>", false)]
    public async Task Response_Should_EchoOnlySafeIncomingCorrelationIds(string incoming, bool echoed)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, incoming);

        var response = await _client.SendAsync(request);

        Assert.That(CorrelationIdOf(response) == incoming, Is.EqualTo(echoed));
    }

    /// <summary>
    /// Validation failures are RFC 9457 problems that carry the request's correlation ID.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InvalidRequest_Should_Return400ProblemWithCorrelationId()
    {
        var response = await _client.PostAsJsonAsync("/Sample", new { name = "", email = "not-an-email" });
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(problem.GetProperty("errors").EnumerateObject().Select(e => e.Name), Is.EquivalentTo(new[] { "Name", "Email" }));
            Assert.That(problem.GetProperty("correlationId").GetString(), Is.EqualTo(CorrelationIdOf(response)));
        }
    }

    /// <summary>
    /// An unhandled exception (here, the database being unreachable) becomes a 500 problem that
    /// keeps the correlation ID in both the header and the body, so it can be matched to the logs.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnhandledException_Should_Return500ProblemWithCorrelationId()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/Sample/{Guid.NewGuid()}");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "failing-request");

        var response = await _client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(CorrelationIdOf(response), Is.EqualTo("failing-request"));
            Assert.That(problem.GetProperty("correlationId").GetString(), Is.EqualTo("failing-request"));
        }
    }

    /// <summary>
    /// Every slice is mapped and described in the OpenAPI document.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OpenApi_Should_DescribeEverySlice()
    {
        var document = await _client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var paths = document.GetProperty("paths").EnumerateObject().Select(p => p.Name);

        Assert.That(paths, Is.SupersetOf(new[] { "/Sample", "/Sample/{Id}" }));
    }

    /// <summary>
    /// Reads the single correlation ID header of a response.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns>The header value.</returns>
    private static string CorrelationIdOf(HttpResponseMessage response)
    {
        return response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
    }
}
