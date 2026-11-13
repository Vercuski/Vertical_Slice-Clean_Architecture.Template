#if (HasDapper)
using Dapper;
#endif
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
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
/// Vertical slice for <c>POST /Sample</c>: creates a sample.
/// </summary>
/// <remarks>
/// The slice's command, response, validator, handler and endpoint are nested in this one class. It
/// writes to the command (write) side of the database. The request carries only what a client may
/// choose; the identifier and the initial state come from <see cref="SampleEntity.Create"/>.
/// </remarks>
public static class PostSample
{
    /// <summary>
    /// The request body for this slice.
    /// </summary>
    /// <param name="Name">The display name of the new sample.</param>
    /// <param name="Email">The email address of the new sample. It must not belong to another sample.</param>
    public sealed record Command(string Name, string Email);

    /// <summary>
    /// The response body returned on success.
    /// </summary>
    /// <param name="Id">The identifier the server assigned to the new sample.</param>
    public sealed record Response(Guid Id);

    /// <summary>
    /// FluentValidation rules for <see cref="Command"/>, applied by <c>WithValidation&lt;Command&gt;()</c>
    /// before the handler runs. The length limits are the entity's, so they match the database mapping.
    /// </summary>
    public sealed class Validator : AbstractValidator<Command>
    {
        /// <summary>
        /// Defines the rules: a <see cref="Command.Name"/> of at most <see cref="SampleEntity.NameMaxLength"/>
        /// characters, and a valid <see cref="Command.Email"/> of at most <see cref="SampleEntity.EmailMaxLength"/> characters.
        /// Each property stops at its first failure, so an empty email is reported once, as empty.
        /// </summary>
        public Validator()
        {
            RuleFor(c => c.Name).Cascade(CascadeMode.Stop)
                .NotEmpty()
                .MaximumLength(SampleEntity.NameMaxLength);
            RuleFor(c => c.Email).Cascade(CascadeMode.Stop)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(SampleEntity.EmailMaxLength);
        }
    }

#if (HasDapper)
    /// <summary>
    /// Inserts the sample with Dapper against the command database.
    /// </summary>
    /// <param name="connectionFactory">Creates connections to the command database.</param>
    public sealed class Handler(IDbWriteConnectionFactory connectionFactory) : ISliceHandler
    {
        /// <summary>
        /// Counts samples with the given email address. Identifiers are unquoted so the same SQL runs on every supported engine.
        /// </summary>
        private const string EmailInUseSql = "SELECT COUNT(1) FROM Samples WHERE Email = @Email";

        /// <summary>
        /// Inserts one sample.
        /// </summary>
        private const string InsertSql = "INSERT INTO Samples (Id, Name, Email, IsActive) VALUES (@Id, @Name, @Email, @IsActive)";

        /// <summary>
        /// Creates the sample unless its email address is already in use.
        /// </summary>
        /// <param name="command">The command that holds the new sample's details.</param>
        /// <param name="cancellationToken">A token that cancels the database calls.</param>
        /// <returns>
        /// <c>201 Created</c> with a <c>Location</c> of <c>/Sample/{Id}</c>, or <c>409 Conflict</c>
        /// with a problem body if another sample already has the email address.
        /// </returns>
        /// <remarks>
        /// The check reads from the command database, not the query database, so it is not affected
        /// by replication lag when the read side is a replica. Two requests racing with the same email
        /// can both pass it; the unique index on <c>Email</c> then rejects the second insert.
        /// </remarks>
        public async Task<Results<Created<Response>, Conflict<ProblemDetails>>> Handle(
            Command command,
            CancellationToken cancellationToken)
        {
            using var connection = connectionFactory.CreateConnection();

            var emailInUse = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(EmailInUseSql, new { command.Email }, cancellationToken: cancellationToken));
            if (emailInUse > 0)
            {
                return Problems.Conflict("Email already in use", $"Another sample already uses {command.Email}.");
            }

            var sample = SampleEntity.Create(command.Name, command.Email);
            await connection.ExecuteAsync(
                new CommandDefinition(InsertSql, sample, cancellationToken: cancellationToken));

            return TypedResults.Created($"/Sample/{sample.Id}", new Response(sample.Id));
        }
    }
#else
    /// <summary>
    /// Adds the sample through <see cref="CommandDbContext"/>.
    /// </summary>
    /// <param name="db">The write-side database context.</param>
    public sealed class Handler(CommandDbContext db) : ISliceHandler
    {
        /// <summary>
        /// Creates the sample unless its email address is already in use.
        /// </summary>
        /// <param name="command">The command that holds the new sample's details.</param>
        /// <param name="cancellationToken">A token that cancels the database calls.</param>
        /// <returns>
        /// <c>201 Created</c> with a <c>Location</c> of <c>/Sample/{Id}</c>, or <c>409 Conflict</c>
        /// with a problem body if another sample already has the email address.
        /// </returns>
        /// <remarks>
        /// Two requests racing with the same email can both pass the check; the unique index on
        /// <c>Email</c> then rejects the second save.
        /// </remarks>
        public async Task<Results<Created<Response>, Conflict<ProblemDetails>>> Handle(
            Command command,
            CancellationToken cancellationToken)
        {
            if (await db.Samples.AnyAsync(s => s.Email == command.Email, cancellationToken))
            {
                return Problems.Conflict("Email already in use", $"Another sample already uses {command.Email}.");
            }

            var sample = SampleEntity.Create(command.Name, command.Email);
            db.Samples.Add(sample);
            await db.SaveChangesAsync(cancellationToken);

            return TypedResults.Created($"/Sample/{sample.Id}", new Response(sample.Id));
        }
    }
#endif

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
