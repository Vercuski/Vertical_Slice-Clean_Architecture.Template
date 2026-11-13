using System.Diagnostics.CodeAnalysis;

namespace Vertical_Slice_Clean_Architecture.Domain.Abstractions;

/// <summary>
/// Abstract base class for domain entities: every entity has a <see cref="Id"/> that identifies it.
/// </summary>
/// <remarks>
/// <para>
/// Add members here that every entity should share, for example audit fields or domain events.
/// </para>
/// <para>
/// <see cref="Id"/> has a protected setter, so only the entity itself (typically in a factory
/// method) assigns it. EF Core and Dapper can still set it when they load a row, because both
/// write to non-public setters.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public abstract class Entity : IEntity
{
    /// <summary>
    /// Gets the unique identifier of the entity.
    /// </summary>
    public Guid Id { get; protected set; }
}
