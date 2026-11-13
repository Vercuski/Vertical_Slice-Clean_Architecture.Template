using Vertical_Slice_Clean_Architecture.Domain.Entities;

namespace Vertical_Slice_Clean_Architecture.Tests.UnitTests;

/// <summary>
/// Unit tests for the <see cref="SampleEntity"/> factory and behavior.
/// </summary>
[TestFixture]
public class SampleEntityTests
{
    /// <summary>
    /// A new sample is active, keeps its values, and gets a version 7 (time-ordered) identifier.
    /// </summary>
    [Test]
    public void Create_Should_ReturnAnActiveSampleWithAVersion7Id()
    {
        var sample = SampleEntity.Create("Sample one", "one@example.com");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sample.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(sample.Id.Version, Is.EqualTo(7));
            Assert.That(sample.Name, Is.EqualTo("Sample one"));
            Assert.That(sample.Email, Is.EqualTo("one@example.com"));
            Assert.That(sample.IsActive, Is.True);
        }
    }

    /// <summary>
    /// Identifiers created later sort after earlier ones, which keeps primary-key inserts sequential.
    /// </summary>
    [Test]
    public void Create_Should_ReturnIncreasingIds()
    {
        var first = SampleEntity.Create("First", "first@example.com");
        Thread.Sleep(2);
        var second = SampleEntity.Create("Second", "second@example.com");

        Assert.That(second.Id, Is.GreaterThan(first.Id));
    }

    /// <summary>
    /// Missing or blank values are rejected by the entity itself, not only by request validation.
    /// </summary>
    /// <param name="name">The name to try.</param>
    /// <param name="email">The email to try.</param>
    [TestCase("", "one@example.com")]
    [TestCase("   ", "one@example.com")]
    [TestCase("Sample one", "")]
    [TestCase("Sample one", "   ")]
    public void Create_Should_RejectBlankValues(string name, string email)
    {
        Assert.That(() => SampleEntity.Create(name, email), Throws.InstanceOf<ArgumentException>());
    }

    /// <summary>
    /// Values longer than the mapped column lengths are rejected.
    /// </summary>
    [Test]
    public void Create_Should_RejectValuesLongerThanTheirMaximum()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                () => SampleEntity.Create(new string('n', SampleEntity.NameMaxLength + 1), "one@example.com"),
                Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => SampleEntity.Create("Sample one", new string('e', SampleEntity.EmailMaxLength + 1)),
                Throws.InstanceOf<ArgumentOutOfRangeException>());
        }
    }

    /// <summary>
    /// <see cref="SampleEntity.Deactivate"/> marks the sample inactive.
    /// </summary>
    [Test]
    public void Deactivate_Should_MarkTheSampleInactive()
    {
        var sample = SampleEntity.Create("Sample one", "one@example.com");

        sample.Deactivate();

        Assert.That(sample.IsActive, Is.False);
    }
}
