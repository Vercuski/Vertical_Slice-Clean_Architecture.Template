using Vertical_Slice_Clean_Architecture.Domain.Entities;
using Vertical_Slice_Clean_Architecture.Presentation.API.Features.Sample;

namespace Vertical_Slice_Clean_Architecture.Tests.UnitTests;

/// <summary>
/// Unit tests for the <see cref="PostSample.Validator"/> request rules.
/// </summary>
[TestFixture]
public class PostSampleValidatorTests
{
    /// <summary>
    /// The validator under test.
    /// </summary>
    private readonly PostSample.Validator _validator = new();

    /// <summary>
    /// A well-formed request passes.
    /// </summary>
    [Test]
    public void Validate_Should_AcceptAValidRequest()
    {
        var result = _validator.Validate(new PostSample.Command("Sample one", "one@example.com"));

        Assert.That(result.IsValid, Is.True);
    }

    /// <summary>
    /// Each invalid field is reported against that field.
    /// </summary>
    /// <param name="name">The name to try.</param>
    /// <param name="email">The email to try.</param>
    /// <param name="invalidProperty">The property expected to fail.</param>
    [TestCase("", "one@example.com", nameof(PostSample.Command.Name))]
    [TestCase("Sample one", "", nameof(PostSample.Command.Email))]
    [TestCase("Sample one", "not-an-email", nameof(PostSample.Command.Email))]
    public void Validate_Should_RejectAnInvalidField(string name, string email, string invalidProperty)
    {
        var result = _validator.Validate(new PostSample.Command(name, email));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Select(e => e.PropertyName), Is.EquivalentTo(new[] { invalidProperty }));
        }
    }

    /// <summary>
    /// The length limits are the entity's, so a request that passes validation also fits the database.
    /// </summary>
    [Test]
    public void Validate_Should_RejectValuesLongerThanTheEntityAllows()
    {
        var tooLongName = new string('n', SampleEntity.NameMaxLength + 1);
        var tooLongEmail = new string('e', SampleEntity.EmailMaxLength - "@example.com".Length + 1) + "@example.com";

        var result = _validator.Validate(new PostSample.Command(tooLongName, tooLongEmail));

        Assert.That(
            result.Errors.Select(e => e.PropertyName).Distinct(),
            Is.EquivalentTo(new[] { nameof(PostSample.Command.Name), nameof(PostSample.Command.Email) }));
    }
}
