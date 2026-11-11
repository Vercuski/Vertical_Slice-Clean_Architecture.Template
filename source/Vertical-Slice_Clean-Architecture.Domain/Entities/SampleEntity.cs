using Vertical_Slice_Clean_Architecture.Domain.Abstractions;

namespace Vertical_Slice_Clean_Architecture.Domain.Entities;

/// <summary>
/// Example domain entity used by the <c>Sample</c> feature slices to show how an entity
/// moves from the domain layer, through persistence, to the API.
/// </summary>
/// <remarks>
/// Replace or rename this type when you build real features from the template.
/// </remarks>
public sealed record SampleEntity : IEntity
{
    /// <summary>
    /// Gets or sets the unique identifier of the sample. The caller supplies it; the database does not generate it.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the display name of the sample.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the email address associated with the sample.
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the sample is active. Defaults to <see langword="true"/>.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
