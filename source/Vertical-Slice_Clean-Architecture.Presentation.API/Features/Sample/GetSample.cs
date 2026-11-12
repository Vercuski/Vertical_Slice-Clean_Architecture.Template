using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Vertical_Slice_Clean_Architecture.Domain.Entities;
using Vertical_Slice_Clean_Architecture.Infrastructure.Contexts;
using Vertical_Slice_Clean_Architecture.Presentation.API.Common;

namespace Vertical_Slice_Clean_Architecture.Presentation.API.Features.Sample;

/// <summary>
/// Vertical slice for <c>GET /Sample/{Id}</c>: returns a single <see cref="SampleEntity"/> by its identifier.
/// </summary>
/// <remarks>
/// The slice's request, response, handler and endpoint are nested in this one class.
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
    /// <param name="Samples">The matching samples. It contains exactly one item when returned by <see cref="Handler"/>.</param>
    public sealed record Response(
        IList<SampleEntity> Samples);

    /// <summary>
    /// Runs the query against <see cref="SampleDbContext"/>.
    /// </summary>
    /// <param name="db">The database context to read from.</param>
    public sealed class Handler(SampleDbContext db) : ISliceHandler
    {
        /// <summary>
        /// Looks up the sample with the requested identifier, without change tracking.
        /// </summary>
        /// <param name="query">The query that holds the identifier to look up.</param>
        /// <param name="cancellationToken">A token that cancels the database call.</param>
        /// <returns><c>200 OK</c> with a <see cref="Response"/> if the sample exists; otherwise <c>404 Not Found</c>.</returns>
        public async Task<Results<Ok<Response>, NotFound>> Handle(Query query, CancellationToken cancellationToken)
        {
            var response = await db.Samples
                .AsNoTracking()
                .Where(o => o.Id == query.Id)
                .Select(o => new Response(new List<SampleEntity> { o })
                ).SingleOrDefaultAsync(cancellationToken);

            return response is null ? TypedResults.NotFound() : TypedResults.Ok(response);
        }
    }

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
