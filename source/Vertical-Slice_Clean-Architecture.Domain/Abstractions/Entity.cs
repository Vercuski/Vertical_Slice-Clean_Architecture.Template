using System.Diagnostics.CodeAnalysis;

namespace Vertical_Slice_Clean_Architecture.Domain.Abstractions;

/// <summary>
/// Optional abstract base class for domain entities that are modelled as classes.
/// </summary>
/// <remarks>
/// It carries no state or behavior yet. It exists as a single place to add members that every
/// class-based entity should share (for example an identifier, audit fields or domain events).
/// Entities written as records, such as <c>SampleEntity</c>, implement <see cref="IEntity"/> directly instead.
/// </remarks>
[ExcludeFromCodeCoverage]
public abstract class Entity : IEntity
{
}
