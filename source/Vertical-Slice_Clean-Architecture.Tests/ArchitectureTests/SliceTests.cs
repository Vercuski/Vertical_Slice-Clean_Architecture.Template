using NetArchTest.Rules;
using Vertical_Slice_Clean_Architecture.Domain.Abstractions;
using Vertical_Slice_Clean_Architecture.Presentation.API.Common;

namespace Vertical_Slice_Clean_Architecture.Tests.ArchitectureTests;

/// <summary>
/// Enforces the vertical slice conventions: each feature is self-contained, and its parts follow
/// the shapes the slice infrastructure discovers.
/// </summary>
[TestFixture]
public class SliceTests
{
    /// <summary>
    /// Gets every feature namespace (the child namespaces of <c>Features</c>), for example <c>Features.Sample</c>.
    /// </summary>
    private static IEnumerable<string> FeatureNamespaces => Layers.Api.GetTypes()
        .Select(type => type.Namespace)
        .OfType<string>()
        .Where(ns => ns.StartsWith(Layers.FeaturesNamespace + ".", StringComparison.Ordinal))
        .Select(ns => string.Join('.', ns.Split('.').Take(Layers.FeaturesNamespace.Split('.').Length + 1)))
        .Distinct()
        .Order();

    /// <summary>
    /// A feature must not use another feature's types. Shared code belongs in <c>Common</c>, the
    /// Domain or Infrastructure, so features can change, move or be deleted independently.
    /// </summary>
    /// <param name="feature">The feature namespace under test.</param>
    [TestCaseSource(nameof(FeatureNamespaces))]
    public void Feature_Should_NotDependOnOtherFeatures(string feature)
    {
        var otherFeatures = FeatureNamespaces.Where(ns => ns != feature).ToArray();
        if (otherFeatures.Length == 0)
        {
            Assert.Pass("Only one feature exists, so there is nothing it could depend on yet.");
        }

        var result = Types.InAssembly(Layers.Api)
            .That().ResideInNamespace(feature)
            .ShouldNot().HaveDependencyOnAny(otherFeatures)
            .GetResult();

        Assert.That(result.FailingTypeNames ?? [], Is.Empty);
    }

    /// <summary>
    /// Endpoints and handlers live under <c>Features</c> and are sealed, so <c>AddSlices</c> finds
    /// every one of them and none is extended elsewhere.
    /// </summary>
    [Test]
    public void EndpointsAndHandlers_Should_BeSealedAndLiveInFeatures()
    {
        var result = Types.InAssembly(Layers.Api)
            .That().ImplementInterface(typeof(IEndpoint))
            .Or().ImplementInterface(typeof(ISliceHandler))
            .Should().BeSealed()
            .And().ResideInNamespace(Layers.FeaturesNamespace)
            .GetResult();

        Assert.That(result.FailingTypeNames ?? [], Is.Empty);
    }

    /// <summary>
    /// Domain entities derive from <see cref="Entity"/> (which gives them an identity) and are sealed.
    /// </summary>
    [Test]
    public void DomainEntities_Should_DeriveFromEntityAndBeSealed()
    {
        var result = Types.InAssembly(Layers.Domain)
            .That().ResideInNamespace(typeof(Entity).Namespace!.Replace(".Abstractions", ".Entities", StringComparison.Ordinal))
            .Should().Inherit(typeof(Entity))
            .And().BeSealed()
            .GetResult();

        Assert.That(result.FailingTypeNames ?? [], Is.Empty);
    }
}
