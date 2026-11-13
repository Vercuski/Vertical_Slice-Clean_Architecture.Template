using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Vertical_Slice_Clean_Architecture.Presentation.API.Features.Sample;

namespace Vertical_Slice_Clean_Architecture.Tests.IntegrationTests.Database;

/// <summary>
/// Runs the <c>Sample</c> slices end to end against a real database of this solution's engine,
/// through whichever data access (EF Core or Dapper) the solution uses.
/// </summary>
/// <remarks>
/// Every test uses its own email address, so the tests are independent of each other and of rows
/// left by earlier runs.
/// </remarks>
[TestFixture]
public class SampleSliceTests
{
    /// <summary>
    /// A client for the API connected to the test database.
    /// </summary>
    private HttpClient _client = null!;

    /// <summary>
    /// Creates the client.
    /// </summary>
    [OneTimeSetUp]
    public void CreateClient()
    {
        _client = DatabaseSetUp.Factory.CreateClient();
    }

    /// <summary>
    /// Disposes the client.
    /// </summary>
    [OneTimeTearDown]
    public void DisposeClient()
    {
        _client.Dispose();
    }

    /// <summary>
    /// A created sample can be read back, at the location the POST returned.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Post_Then_Get_Should_RoundTripTheSample()
    {
        var email = UniqueEmail();

        var created = await _client.PostAsJsonAsync("/Sample", new PostSample.Command("Round trip", email));
        var id = (await created.Content.ReadFromJsonAsync<PostSample.Response>())!.Id;
        var fetched = await _client.GetFromJsonAsync<GetSample.Response>(created.Headers.Location);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(created.Headers.Location?.ToString(), Is.EqualTo($"/Sample/{id}"));
            Assert.That(fetched, Is.EqualTo(new GetSample.Response(id, "Round trip", email, IsActive: true)));
        }
    }

    /// <summary>
    /// The server assigns the identifier and initial state; values a client sends for them are ignored.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Post_Should_IgnoreClientSuppliedIdAndState()
    {
        var clientId = Guid.NewGuid();

        var created = await _client.PostAsJsonAsync("/Sample", new { id = clientId, isActive = false, name = "Over-post", email = UniqueEmail() });
        var id = (await created.Content.ReadFromJsonAsync<PostSample.Response>())!.Id;
        var fetched = await _client.GetFromJsonAsync<GetSample.Response>($"/Sample/{id}");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(id, Is.Not.EqualTo(clientId));
            Assert.That(fetched!.IsActive, Is.True);
        }
    }

    /// <summary>
    /// A second sample with the same email is rejected with a 409 problem carrying the correlation ID.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Post_Should_Return409_WhenTheEmailIsInUse()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/Sample", new PostSample.Command("First", email));

        var duplicate = await _client.PostAsJsonAsync("/Sample", new PostSample.Command("Second", email));
        var problem = await duplicate.Content.ReadFromJsonAsync<JsonElement>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(duplicate.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(problem.GetProperty("title").GetString(), Is.EqualTo("Email already in use"));
            Assert.That(problem.GetProperty("correlationId").GetString(), Is.Not.Empty);
        }
    }

    /// <summary>
    /// An unknown identifier is a 404.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Get_Should_Return404_ForAnUnknownId()
    {
        var response = await _client.GetAsync($"/Sample/{Guid.NewGuid()}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    /// <summary>
    /// With the database up, <c>/health</c> is healthy and both sides are reachable.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Health_Should_BeHealthy_WhenTheDatabaseIsUp()
    {
        var response = await _client.GetAsync("/health");
        var check = (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("results").GetProperty("DatabaseHealthCheck");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(check.GetProperty("data").GetProperty("commandDatabase").GetString(), Is.EqualTo("Reachable"));
            Assert.That(check.GetProperty("data").GetProperty("queryDatabase").GetString(), Is.EqualTo("Reachable"));
        }
    }

    /// <summary>
    /// Returns an email address no other test or earlier run has used.
    /// </summary>
    /// <returns>The address.</returns>
    private static string UniqueEmail()
    {
        return $"{Guid.NewGuid():N}@example.com";
    }
}
