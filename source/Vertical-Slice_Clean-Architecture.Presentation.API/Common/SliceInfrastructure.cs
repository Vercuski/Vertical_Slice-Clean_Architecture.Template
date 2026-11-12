using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Vertical_Slice_Clean_Architecture.Presentation.API.Common;

/// <summary>
/// Implemented by a feature slice to register its own HTTP route(s).
/// </summary>
/// <remarks>
/// <see cref="SliceRegistration.AddSlices"/> discovers and registers implementations, and
/// <see cref="SliceRegistration.MapSlices"/> maps them, so a new slice needs no changes to <c>Program.cs</c>.
/// </remarks>
public interface IEndpoint
{
    /// <summary>
    /// Maps this slice's route(s) onto the application's routing table.
    /// </summary>
    /// <param name="app">The route builder to add the endpoint(s) to.</param>
    void MapEndpoint(IEndpointRouteBuilder app);
}

/// <summary>
/// Marker interface for a feature slice's request handler.
/// </summary>
/// <remarks>
/// <see cref="SliceRegistration.AddSlices"/> registers every concrete implementation as a scoped
/// service under its own concrete type, so endpoints can take the handler as a parameter.
/// </remarks>
public interface ISliceHandler;

/// <summary>
/// Extension methods that discover, register and map vertical slices by assembly scanning.
/// </summary>
public static class SliceRegistration
{
    /// <summary>
    /// Scans <paramref name="assembly"/> and registers every slice component it finds.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="assembly">The assembly to scan, usually the API assembly.</param>
    /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
    /// <remarks>
    /// Only concrete, non-generic classes are considered:
    /// <list type="bullet">
    ///   <item><description>Each <see cref="ISliceHandler"/> is registered as a scoped service under its concrete type.</description></item>
    ///   <item><description>Each <see cref="IEndpoint"/> is registered as a singleton <see cref="IEndpoint"/>. <c>TryAddEnumerable</c> prevents duplicates if this method is called more than once.</description></item>
    ///   <item><description>All FluentValidation validators, including internal ones, are registered.</description></item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddSlices(this IServiceCollection services, Assembly assembly)
    {
        var concreteTypes = assembly.DefinedTypes
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false });

        foreach (var type in concreteTypes)
        {
            if (typeof(ISliceHandler).IsAssignableFrom(type))
            {
                services.AddScoped(type);
            }

            if (typeof(IEndpoint).IsAssignableFrom(type))
            {
                services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IEndpoint), type));
            }
        }

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        return services;
    }

    /// <summary>
    /// Resolves every registered <see cref="IEndpoint"/> and calls <see cref="IEndpoint.MapEndpoint"/> on it.
    /// </summary>
    /// <param name="app">The route builder (typically the <c>WebApplication</c>) to map routes onto.</param>
    /// <returns>The same <paramref name="app"/> instance, for chaining.</returns>
    /// <remarks>Call this after the application is built and after <see cref="AddSlices"/> has run.</remarks>
    public static IEndpointRouteBuilder MapSlices(this IEndpointRouteBuilder app)
    {
        foreach (var endpoint in app.ServiceProvider.GetServices<IEndpoint>())
        {
            endpoint.MapEndpoint(app);
        }

        return app;
    }

    /// <summary>
    /// Adds FluentValidation of the <typeparamref name="TRequest"/> argument to a route, and declares
    /// in OpenAPI that the route can return a validation problem response.
    /// </summary>
    /// <typeparam name="TRequest">The request type to validate. An <see cref="IValidator{T}"/> for it must be registered.</typeparam>
    /// <param name="builder">The route handler builder to add the filter to.</param>
    /// <returns>The same <paramref name="builder"/> instance, for chaining.</returns>
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter<ValidationFilter<TRequest>>().ProducesValidationProblem();
    }
}

/// <summary>
/// Endpoint filter that validates the <typeparamref name="TRequest"/> argument with FluentValidation
/// before the route handler runs.
/// </summary>
/// <typeparam name="TRequest">The request type to validate.</typeparam>
public sealed class ValidationFilter<TRequest> : IEndpointFilter
{
    /// <summary>
    /// Validates the request and either short-circuits with an error response or calls the next step in the pipeline.
    /// </summary>
    /// <param name="context">The filter context, which holds the handler arguments and the current <see cref="HttpContext"/>.</param>
    /// <param name="next">The next filter, or the route handler itself.</param>
    /// <returns>
    /// <list type="bullet">
    ///   <item><description>A <c>400 Bad Request</c> problem response if no <typeparamref name="TRequest"/> argument is present.</description></item>
    ///   <item><description>A <c>400</c> validation problem response listing the errors if validation fails.</description></item>
    ///   <item><description>Otherwise, the result of <paramref name="next"/>.</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="InvalidOperationException">No <see cref="IValidator{T}"/> for <typeparamref name="TRequest"/> is registered.</exception>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            return TypedResults.Problem(
                title: "Request body is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var validator = context.HttpContext.RequestServices.GetRequiredService<IValidator<TRequest>>();
        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);

        return result.IsValid
            ? await next(context)
            : TypedResults.ValidationProblem(result.ToDictionary());
    }
}

/// <summary>
/// Factory methods for typed RFC 9457 <see cref="ProblemDetails"/> error responses, so every slice reports errors in the same shape.
/// </summary>
public static class Problems
{
    /// <summary>
    /// Creates a <c>404 Not Found</c> response with a <see cref="ProblemDetails"/> body.
    /// </summary>
    /// <param name="title">A short, human-readable summary of the problem.</param>
    /// <param name="detail">An explanation specific to this occurrence of the problem.</param>
    /// <returns>A typed <c>404</c> result.</returns>
    public static NotFound<ProblemDetails> NotFound(string title, string detail)
    {
        return TypedResults.NotFound(new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = StatusCodes.Status404NotFound,
        });
    }

    /// <summary>
    /// Creates a <c>409 Conflict</c> response with a <see cref="ProblemDetails"/> body.
    /// </summary>
    /// <param name="title">A short, human-readable summary of the problem.</param>
    /// <param name="detail">An explanation specific to this occurrence of the problem.</param>
    /// <returns>A typed <c>409</c> result.</returns>
    public static Conflict<ProblemDetails> Conflict(string title, string detail)
    {
        return TypedResults.Conflict(new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = StatusCodes.Status409Conflict,
        });
    }
}
