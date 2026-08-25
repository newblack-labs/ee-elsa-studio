namespace Elsa.Studio.Services;

/// <summary>
/// Reconciles the feature names Studio modules declare with the names a host reports.
/// </summary>
/// <remarks>
/// Shared by <see cref="RemoteFeatureProvider"/>, which decides whether a menu contributes, and
/// <see cref="DefaultFeatureService"/>, which decides whether a feature is initialized. Those two must
/// agree: matching in one place only produces a menu entry pointing at a feature that was never
/// initialized, which is a subtler version of the bug the reconciliation exists to fix.
/// </remarks>
public static class RemoteFeatureNames
{
    private const string ShellFeatureMarker = ".ShellFeatures.";

    /// <summary>
    /// Returns every name a host might report for the given declared feature name.
    /// </summary>
    /// <remarks>
    /// A CShells host reports the declared name verbatim. A classic host composes its own in
    /// Module.Install, as the literal namespace "Elsa" plus the feature type's name with "Feature"
    /// stripped — so "Elsa.Diagnostics.ConsoleLogs.ShellFeatures.ConsoleLogs" arrives as
    /// "Elsa.ConsoleLogs". The module name is offered too, for modules already one segment deep.
    /// </remarks>
    public static HashSet<string> GetCandidates(string featureName)
    {
        var candidates = new HashSet<string>(StringComparer.Ordinal) { featureName };

        var index = featureName.LastIndexOf(ShellFeatureMarker, StringComparison.Ordinal);
        if (index < 0)
            return candidates;

        var moduleName = featureName[..index];
        var shellFeatureName = featureName[(index + ShellFeatureMarker.Length)..];
        candidates.Add(moduleName);

        var rootNamespace = moduleName.Split('.').FirstOrDefault();
        if (!string.IsNullOrEmpty(rootNamespace) && !string.IsNullOrEmpty(shellFeatureName))
            candidates.Add($"{rootNamespace}.{shellFeatureName}");

        return candidates;
    }

    /// <summary>
    /// Determines whether any of the host-reported names matches the declared feature name.
    /// </summary>
    public static bool Matches(string featureName, IEnumerable<string> reportedNames)
    {
        var candidates = GetCandidates(featureName);
        return reportedNames.Any(candidates.Contains);
    }
}
