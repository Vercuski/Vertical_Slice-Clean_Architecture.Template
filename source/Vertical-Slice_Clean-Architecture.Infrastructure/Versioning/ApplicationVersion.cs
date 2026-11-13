using System.Reflection;

namespace Vertical_Slice_Clean_Architecture.Infrastructure.Versioning;

/// <summary>
/// The application's semantic version, read once from the assembly metadata that MinVer
/// stamps at build time (see <c>Directory.Build.props</c>).
/// </summary>
/// <remarks>
/// Every project in the solution is versioned from the same git tags, so all assemblies carry the
/// same version. <see cref="Current"/> therefore reads this (Infrastructure) assembly rather than
/// <see cref="Assembly.GetEntryAssembly"/>, which is the test host under
/// <c>WebApplicationFactory</c> and would report the wrong version. Registered as a singleton by
/// <c>AddInfrastructureRegistration()</c>.
/// </remarks>
public sealed class ApplicationVersion
{
    /// <summary>
    /// The version of the running application, taken from this assembly's
    /// <see cref="AssemblyInformationalVersionAttribute"/>.
    /// </summary>
    public static ApplicationVersion Current { get; } = FromAssembly(typeof(ApplicationVersion).Assembly);

    /// <summary>
    /// Initializes a new instance from a full informational version string.
    /// </summary>
    /// <param name="informationalVersion">The version, optionally followed by <c>+</c> and build metadata.</param>
    private ApplicationVersion(string informationalVersion)
    {
        InformationalVersion = informationalVersion;

        var metadataSeparator = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        SemanticVersion = metadataSeparator >= 0 ? informationalVersion[..metadataSeparator] : informationalVersion;
        BuildMetadata = metadataSeparator >= 0 ? informationalVersion[(metadataSeparator + 1)..] : null;
    }

    /// <summary>
    /// Gets the SemVer 2.0 version without build metadata, e.g. <c>1.4.0</c> or <c>1.4.1-alpha.0.3</c>.
    /// This is the value to show users and compare between builds.
    /// </summary>
    public string SemanticVersion { get; }

    /// <summary>
    /// Gets the full informational version including build metadata, e.g.
    /// <c>1.4.1-alpha.0.3+2057147a69…</c>, where the metadata is the commit SHA the .NET SDK appends.
    /// </summary>
    public string InformationalVersion { get; }

    /// <summary>
    /// Gets the build metadata (the part after <c>+</c>), or <see langword="null"/> if there is none.
    /// </summary>
    public string? BuildMetadata { get; }

    /// <summary>
    /// Gets a value indicating whether this is a pre-release version (it has a <c>-</c> suffix
    /// such as <c>-alpha.0.3</c> or <c>-rc.1</c>).
    /// </summary>
    public bool IsPreRelease => SemanticVersion.Contains('-', StringComparison.Ordinal);

    /// <summary>
    /// Reads the version stamped on <paramref name="assembly"/>.
    /// </summary>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>
    /// The version from <see cref="AssemblyInformationalVersionAttribute"/>, falling back to the
    /// assembly version, or <c>0.0.0</c> if neither is present.
    /// </returns>
    public static ApplicationVersion FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var informationalVersion =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString(3)
            ?? "0.0.0";

        return new ApplicationVersion(informationalVersion);
    }

    /// <summary>
    /// Creates an instance from an informational version string.
    /// </summary>
    /// <param name="informationalVersion">The version, optionally followed by <c>+</c> and build metadata.</param>
    /// <returns>The parsed version.</returns>
    /// <exception cref="ArgumentException"><paramref name="informationalVersion"/> is null, empty, or whitespace.</exception>
    public static ApplicationVersion FromInformationalVersion(string informationalVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(informationalVersion);
        return new ApplicationVersion(informationalVersion);
    }

    /// <summary>
    /// Returns <see cref="SemanticVersion"/>.
    /// </summary>
    /// <returns>The semantic version string.</returns>
    public override string ToString()
    {
        return SemanticVersion;
    }
}
