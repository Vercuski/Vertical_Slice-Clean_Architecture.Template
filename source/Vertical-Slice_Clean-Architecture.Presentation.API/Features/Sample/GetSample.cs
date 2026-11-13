#if (HasDapper)
using Dapper;
#endif
using Microsoft.AspNetCore.Http.HttpResults;
#if (HasEfCore)
using Microsoft.EntityFrameworkCore;
#endif
using Vertical_Slice_Clean_Architecture.Domain.Entities;
#if (HasDapper)
using Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.ConnectionFactory;
#endif
#if (HasEfCore)
using Vertical_Slice_Clean_Architecture.Infrastructure.Persistence.Contexts;
#endif
using Vertical_Slice_Clean_Architecture.Presentation.API.Common;

namespace Vertical_Slice_Clean_Architecture.Presentation.API.Features.Sample;

/// <summary>
/// Vertical slice for <c>GET /Sample/{Id}</c>: returns a single sample by its identifier.
/// </summary>
/// <remarks>
/// The slice's request, response, handler and endpoint are nested in this one class. It reads
/// from the query (read) side of the database and projects straight into <see cref="Response"/>,
/// so no <see cref="SampleEntity"/> is materialized and the domain type never reaches the HTTP contract.
/// </remarks>
public static class GetSample
{
    /// <summary>
    /// The request for this slice.
    /// </summary>
    /// <param name="Id">The identifier of the sample to fetch.</param>
    public sealed record Query(Guid Id);

    /// <summary>
    /// The response body returned when the sample is found.
    /// </summary>
    /// <param name="Id">The identifier of the sample.</param>
    /// <param name="Name">The display name of the sample.</param>
    /// <param name="Email">The email address of the sample.</param>
    /// <param name="IsActive">Whether the sample is active.</param>
    public sealed record Response(Guid Id, string Name, string Email, bool IsActive);

#if (HasDapper)
    /// <summary>
    /// Runs the query with Dapper against the query database.
    /// </summary>
    /// <param name="connectionFactory">Creates connections to the query database.</param>
    public sealed class Handler(IDbReadOnlyConnectionFactory connectionFactory) : ISliceHandler
    {
        /// <summary>
        /// The parameterized query. Its columns are in the same order as the parameters of
        /// <see cref="Response"/>'s constructor, which Dapper requires to map them. Identifiers are
        /// unquoted so the same SQL runs on every supported engine.
        /// </summary>
        private const string Sql = "SELECT Id, Name, Email, IsActive FROM Samples WHERE Id = @Id";

        /// <summary>
        /// Looks up the sample with the requested identifier.
        /// </summary>
        /// <param name="query">The query that holds the identifier to look up.</param>
        /// <param name="cancellationToken">A token that cancels the database call.</param>
        /// <returns><c>200 OK</c> with a <see cref="Response"/> if the sample exists; otherwise <c>404 Not Found</c>.</returns>
        public async Task<Results<Ok<Response>, NotFound>> Handle(Query query, CancellationToken cancellationToken)
        {
            using var connection = connectionFactory.CreateConnection();
            var response = await connection.QuerySingleOrDefaultAsync<Response>(
                new CommandDefinition(Sql, new { query.Id }, cancellationToken: cancellationToken));

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
#else
    /// <summary>
    /// Runs the query against <see cref="QueryDbContext"/>.
    /// </summary>
    /// <param name="db">The read-side database context.</param>
    public sealed class Handler(QueryDbContext db) : ISliceHandler
    {
        /// <summary>
        /// Looks up the sample with the requested identifier, selecting only the columns the response needs.
        /// </summary>
        /// <param name="query">The query that holds the identifier to look up.</param>
        /// <param name="cancellationToken">A token that cancels the database call.</param>
        /// <returns><c>200 OK</c> with a <see cref="Response"/> if the sample exists; otherwise <c>404 Not Found</c>.</returns>
        public async Task<Results<Ok<Response>, NotFound>> Handle(Query query, CancellationToken cancellationToken)
        {
            var response = await db.Samples
                .Where(s => s.Id == query.Id)
                .Select(s => new Response(s.Id, s.Name, s.Email, s.IsActive))
                .SingleOrDefaultAsync(cancellationToken);

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }
#endif

    /// <summary>
    /// Registers the <c>GET /Sample/{Id:guid}</c> route for this slice.
    /// </summary>
    public sealed class Endpoint : IEndpoint
    {
        /// <summary>
        /// Maps <c>GET /Sample/{Id:guid}</c> to <see cref="Handler.Handle"/>. The route is named <c>GetSample</c> and tagged <c>Sample</c> in OpenAPI.
        /// </summary>
        /// <param name="app">The route builder to add the endpoint to.</param>
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapGet(
                    "/Sample/{Id:guid}",
                    (Guid Id, Handler handler, CancellationToken cancellationToken) =>
                        handler.Handle(new Query(Id), cancellationToken))
                .WithName(nameof(GetSample))
                .WithTags("Sample");
        }
    }
}
