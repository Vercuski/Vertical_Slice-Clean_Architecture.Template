using System.Reflection;
using Vertical_Slice_Clean_Architecture.Domain.Abstractions;
using Vertical_Slice_Clean_Architecture.Infrastructure.Correlation;
using Vertical_Slice_Clean_Architecture.Presentation.API.Common;

namespace Vertical_Slice_Clean_Architecture.Tests.ArchitectureTests;

/// <summary>
/// The assemblies and root namespaces the architecture tests reason about.
/// </summary>
internal static class Layers
{
    /// <summary>
    /// The Domain assembly: entities and domain abstractions.
    /// </summary>
    public static readonly Assembly Domain = typeof(Entity).Assembly;

    /// <summary>
    /// The Infrastructure assembly: persistence and cross-cutting services.
    /// </summary>
    public static readonly Assembly Infrastructure = typeof(CorrelationIdAccessor).Assembly;

    /// <summary>
    /// The API assembly: the composition root and the feature slices.
    /// </summary>
    public static readonly Assembly Api = typeof(IEndpoint).Assembly;

    /// <summary>
    /// The root namespace of the Infrastructure assembly.
    /// </summary>
    public static readonly string InfrastructureNamespace = Infrastructure.GetName().Name!.Replace('-', '_');

    /// <summary>
    /// The root namespace of the API assembly.
    /// </summary>
    public static readonly string ApiNamespace = Api.GetName().Name!.Replace('-', '_');

    /// <summary>
    /// The namespace that holds the feature slices; each child namespace is one feature.
    /// </summary>
    public static readonly string FeaturesNamespace = ApiNamespace + ".Features";
}
