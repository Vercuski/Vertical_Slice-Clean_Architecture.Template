using Vertical_Slice_Clean_Architecture.Infrastructure.Versioning;

namespace Vertical_Slice_Clean_Architecture.Tests.UnitTests;

/// <summary>
/// Unit tests for <see cref="ApplicationVersion"/> parsing.
/// </summary>
[TestFixture]
public class ApplicationVersionTests
{
    /// <summary>
    /// Build metadata after <c>+</c> is split from the semantic version.
    /// </summary>
    [Test]
    public void FromInformationalVersion_Should_SplitBuildMetadata()
    {
        var version = ApplicationVersion.FromInformationalVersion("1.4.1-alpha.0.3+2057147a69");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(version.SemanticVersion, Is.EqualTo("1.4.1-alpha.0.3"));
            Assert.That(version.BuildMetadata, Is.EqualTo("2057147a69"));
            Assert.That(version.IsPreRelease, Is.True);
            Assert.That(version.ToString(), Is.EqualTo("1.4.1-alpha.0.3"));
        }
    }

    /// <summary>
    /// A release version has no pre-release suffix and no build metadata.
    /// </summary>
    [Test]
    public void FromInformationalVersion_Should_RecognizeAReleaseVersion()
    {
        var version = ApplicationVersion.FromInformationalVersion("2.0.0");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(version.SemanticVersion, Is.EqualTo("2.0.0"));
            Assert.That(version.BuildMetadata, Is.Null);
            Assert.That(version.IsPreRelease, Is.False);
        }
    }

    /// <summary>
    /// The running build's version is stamped by MinVer, so it is never empty.
    /// </summary>
    [Test]
    public void Current_Should_ReportTheBuildsVersion()
    {
        Assert.That(ApplicationVersion.Current.SemanticVersion, Is.Not.Empty);
    }
}
