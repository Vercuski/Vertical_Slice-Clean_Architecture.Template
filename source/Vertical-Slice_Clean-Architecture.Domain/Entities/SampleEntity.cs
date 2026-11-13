using Vertical_Slice_Clean_Architecture.Domain.Abstractions;

namespace Vertical_Slice_Clean_Architecture.Domain.Entities;

/// <summary>
/// Example domain entity used by the <c>Sample</c> feature slices to show how an entity moves from
/// the domain layer, through persistence, to the API.
/// </summary>
/// <remarks>
/// <para>
/// State changes only through the entity's own members (<see cref="Create"/>, <see cref="Deactivate"/>),
/// so it can never hold invalid values. The API never exposes it directly: slices accept and return
/// their own request and response types.
/// </para>
/// <para>
/// Replace or rename this type when you build real features from the template.
/// </para>
/// </remarks>
public sealed class SampleEntity : Entity
{
    /// <summary>
    /// The maximum length of <see cref="Name"/>, shared by the database mapping and request validation.
    /// </summary>
    public const int NameMaxLength = 200;

    /// <summary>
    /// The maximum length of <see cref="Email"/> (the longest valid email address), shared by the
    /// database mapping and request validation.
    /// </summary>
    public const int EmailMaxLength = 320;

    /// <summary>
    /// Used by EF Core and Dapper to materialize rows. Application code uses <see cref="Create"/>.
    /// </summary>
    private SampleEntity()
    {
    }

    /// <summary>
    /// Gets the display name of the sample.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the email address of the sample. It is unique across all samples.
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the sample is active. New samples are active.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Creates a new, active sample with a new time-ordered identifier (UUID version 7, which keeps
    /// primary-key inserts sequential).
    /// </summary>
    /// <param name="name">The display name, at most <see cref="NameMaxLength"/> characters.</param>
    /// <param name="email">The email address, at most <see cref="EmailMaxLength"/> characters.</param>
    /// <returns>The new sample.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> or <paramref name="email"/> is null, empty, whitespace, or too long.
    /// </exception>
    public static SampleEntity Create(string name, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, NameMaxLength, nameof(name));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(email.Length, EmailMaxLength, nameof(email));

        return new SampleEntity
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Email = email,
            IsActive = true,
        };
    }

    /// <summary>
    /// Marks the sample as inactive.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
    }
}
