using NetArchTest.Rules;

namespace Vertical_Slice_Clean_Architecture.Tests.ArchitectureTests;

/// <summary>
/// Enforces the Clean Architecture dependency rule: dependencies point inwards, towards the Domain.
/// </summary>
[TestFixture]
public class LayerDependencyTests
{
    /// <summary>
    /// The Domain must not know about any outer layer or about persistence and web frameworks, so it
    /// stays plain C# that can be reasoned about and tested on its own.
    /// </summary>
    [Test]
    public void Domain_Should_NotDependOnOuterLayersOrFrameworks()
    {
        var result = Types.InAssembly(Layers.Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                Layers.InfrastructureNamespace,
                Layers.ApiNamespace,
                "Microsoft.EntityFrameworkCore",
                "Dapper",
                "Microsoft.AspNetCore")
            .GetResult();

        Assert.That(result.FailingTypeNames ?? [], Is.Empty);
    }

    /// <summary>
    /// Infrastructure serves the API; it must never reach back into the slices or the composition root.
    /// </summary>
    [Test]
    public void Infrastructure_Should_NotDependOnTheApi()
    {
        var result = Types.InAssembly(Layers.Infrastructure)
            .ShouldNot()
            .HaveDependencyOn(Layers.ApiNamespace)
            .GetResult();

        Assert.That(result.FailingTypeNames ?? [], Is.Empty);
    }
}
