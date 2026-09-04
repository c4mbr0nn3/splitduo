using System.Reflection;

namespace SplitDuo.Api.Features.System.Services;

/// <summary>
/// Resolves the running application version. Injectable so tests can stub it and
/// consumers (background service, notification provider, controller) share one source.
/// </summary>
public interface ICurrentVersionProvider
{
    /// <summary>Running version, or null if the entry assembly version cannot be parsed.</summary>
    SemanticVersion? Current { get; }

    /// <summary>Running version as a string, or "0.0.0" fallback.</summary>
    string CurrentString { get; }
}

public class AssemblyCurrentVersionProvider : ICurrentVersionProvider
{
    public SemanticVersion? Current =>
        SemanticVersion.ParseOrNull(Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3));

    public string CurrentString => Current?.ToString() ?? "0.0.0";
}