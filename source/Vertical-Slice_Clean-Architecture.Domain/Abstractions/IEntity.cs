namespace Vertical_Slice_Clean_Architecture.Domain.Abstractions;

/// <summary>
/// Marker interface that identifies a type as a domain entity.
/// </summary>
/// <remarks>
/// It declares no members. Use it to constrain generic code (repositories, specifications,
/// reflection-based scanning) to domain entities only.
/// </remarks>
public interface IEntity
{
}
