using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vertical_Slice_Clean_Architecture.Domain.Entities;
using Vertical_Slice_Clean_Architecture.Infrastructure.Contexts;
using Vertical_Slice_Clean_Architecture.Presentation.API.Common;

namespace Vertical_Slice_Clean_Architecture.Presentation.API.Features.Sample;

/// <summary>
/// Vertical slice for <c>POST /Sample</c>: accepts a <see cref="SampleEntity"/> and returns <c>201 Created</c>
/// when an active sample with the same identifier already exists.
/// </summary>
/// <remarks>
/// The slice's command, response, validator, handler and endpoint are nested in this one class.
/// </remarks>
public static class PostSample
{
    /// <summary>
    /// Maximum number of lines per request. Not currently enforced by <see cref="Validator"/> or used elsewhere.
    /// </summary>
    public const int MaxLines = 50;

    /// <summary>
    /// Maximum quantity per line. Not currently enforced by <see cref="Validator"/> or used elsewhere.
    /// </summary>
    public const int MaxQuantityPerLine = 100;

    /// <summary>
    /// The request body for this slice.
    /// </summary>
    /// <param name="Sample">The sample being submitted.</param>
    public sealed record Command(SampleEntity Sample);

    /// <summary>
    /// The response body returned on success.
    /// </summary>
    /// <param name="Id">The identifier of the sample.</param>
    public sealed record Response(Guid Id);

    /// <summary>
    /// FluentValidation rules for <see cref="Command"/>, applied by <c>WithValidation&lt;Command&gt;()</c> before the handler runs.
    /// </summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>
        /// Defines the rules: <see cref="SampleEntity.Id"/> must not be <see cref="Guid.Empty"/>.
        /// </summary>
        public Validator()
        {
            RuleFor(s => s.Sample.Id).NotEmpty();
        }
    }

    /// <summary>
    /// Processes a <see cref="Command"/> against <see cref="SampleDbContext"/>.
    /// </summary>
    /// <param name="db">The database context to read from and save to.</param>
    public sealed class Handler(SampleDbContext db) : ISliceHandler
    {
        /// <summary>
        /// Checks that an active sample with the submitted identifier exists, then saves pending changes.
        /// </summary>
        /// <param name="command">The command that holds the submitted sample.</param>
        /// <param name="cancellationToken">A token that cancels the database calls.</param>
        /// <returns>
        /// <list type="bullet">
        ///   <item><description><c>404 Not Found</c> with a problem body if no active sample with that identifier exists.</description></item>
        ///   <item><description><c>409 Conflict</c> with a problem body if the save fails with a <see cref="DbUpdateConcurrencyException"/>.</description></item>
        ///   <item><description>Otherwise <c>201 Created</c> with a <c>Location</c> of <c>/Sample/{Id}</c> and a <see cref="Response"/> body.</description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// The handler does not add <see cref="Command.Sample"/> to the context, so <c>SaveChangesAsync</c>
        /// currently has nothing to save.
        /// </remarks>
        public async Task<Results<Created<Response>, NotFound<ProblemDetails>, Conflict<ProblemDetails>>> Handle(
            Command command,
            CancellationToken cancellationToken)
        {
            var customerIsActive = await db.Samples
                .AnyAsync(c => c.Id == command.Sample.Id && c.IsActive, cancellationToken);

            if (!customerIsActive)
            {
                return Problems.NotFound(
                    "Sample not found",
                    $"No active sample exists with id {command.Sample.Id}.");
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Problems.Conflict(
                    "Sample changed",
                    "Sample changed. Retry the request.");
            }

            return TypedResults.Created(
                $"/Sample/{command.Sample.Id}",
                new Response(command.Sample.Id));
        }
    }

    /// <summary>
    /// Registers the <c>POST /Sample</c> route for this slice.
    /// </summary>
    public sealed class Endpoint : IEndpoint
    {
        /// <summary>
        /// Maps <c>POST /Sample</c> to <see cref="Handler.Handle"/>, with <see cref="Validator"/> run first.
        /// The route is named <c>PostSample</c> and tagged <c>Sample</c> in OpenAPI.
        /// </summary>
        /// <param name="app">The route builder to add the endpoint to.</param>
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost(
                    "/Sample",
                    (Command command, Handler handler, CancellationToken cancellationToken) =>
                        handler.Handle(command, cancellationToken))
                .WithName(nameof(PostSample))
                .WithTags("Sample")
                .WithValidation<Command>();
        }
    }
}
