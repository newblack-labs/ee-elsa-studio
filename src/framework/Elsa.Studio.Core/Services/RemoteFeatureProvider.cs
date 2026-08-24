using System.Net;
using Elsa.Api.Client.Resources.Features.Contracts;
using Elsa.Api.Client.Resources.Features.Models;
using Elsa.Studio.Contracts;
using Microsoft.AspNetCore.Components.Authorization;
using Refit;

namespace Elsa.Studio.Services;

/// <summary>
/// A feature service that uses a remote backend to retrieve feature flags.
/// </summary>
public class RemoteFeatureProvider(
    IBackendApiClientProvider remoteBackendApiClientProvider,
    AuthenticationStateProvider? authenticationStateProvider = null) : IRemoteFeatureProvider
{
    private readonly SemaphoreSlim _catalogLock = new(1, 1);
    private IReadOnlyCollection<FeatureDescriptor>? _catalog;

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default)
    {
        var catalog = await GetCatalogAsync(cancellationToken);
        var candidates = GetCandidateNames(featureName);

        return catalog.Any(feature => candidates.Contains(feature.FullName));
    }

    /// <summary>
    /// Returns the names a host might report for the requested feature, newest convention first.
    /// </summary>
    /// <remarks>
    /// Studio modules declare their server dependency using the CShells shell-feature name, such as
    /// "Elsa.Alterations.ShellFeatures.Alterations" or
    /// "Elsa.Diagnostics.ConsoleLogs.ShellFeatures.ConsoleLogs". A classic Elsa host installs the same
    /// capabilities under shorter names — "Elsa.Alterations" and "Elsa.ConsoleLogs" — so an exact match
    /// never succeeds there and every module carrying [RemoteFeature] quietly disables itself: its menu
    /// contributes nothing even though the server supports the feature and its pages route correctly.
    ///
    /// Two shapes have to be accepted, because dropping ".ShellFeatures.&lt;name&gt;" is not always enough:
    /// the classic name also loses intermediate namespace segments, so
    /// "Elsa.Diagnostics.ConsoleLogs.ShellFeatures.ConsoleLogs" becomes "Elsa.ConsoleLogs" rather than
    /// "Elsa.Diagnostics.ConsoleLogs". Offer both, plus "Elsa.&lt;feature&gt;" built from the leading
    /// namespace and the shell-feature name.
    /// </remarks>
    private static HashSet<string> GetCandidateNames(string featureName)
    {
        var candidates = new HashSet<string>(StringComparer.Ordinal) { featureName };

        const string marker = ".ShellFeatures.";
        var index = featureName.LastIndexOf(marker, StringComparison.Ordinal);

        if (index < 0)
            return candidates;

        var moduleName = featureName[..index];
        var shellFeatureName = featureName[(index + marker.Length)..];
        candidates.Add(moduleName);

        // "Elsa.Diagnostics.ConsoleLogs" + "ConsoleLogs" -> "Elsa.ConsoleLogs".
        var rootNamespace = moduleName.Split('.').FirstOrDefault();
        if (!string.IsNullOrEmpty(rootNamespace) && !string.IsNullOrEmpty(shellFeatureName))
            candidates.Add($"{rootNamespace}.{shellFeatureName}");

        return candidates;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FeatureDescriptor>> ListAsync(CancellationToken cancellationToken = default) =>
        await GetCatalogAsync(cancellationToken);

    private async Task<IReadOnlyCollection<FeatureDescriptor>> GetCatalogAsync(CancellationToken cancellationToken)
    {
        if (authenticationStateProvider is not null)
        {
            var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
            if (authenticationState.User.Identity?.IsAuthenticated != true)
                return [];
        }

        if (_catalog is not null)
            return _catalog;

        await _catalogLock.WaitAsync(cancellationToken);
        try
        {
            if (_catalog is not null)
                return _catalog;

            var api = await remoteBackendApiClientProvider.GetApiAsync<IFeaturesApi>(cancellationToken);
            try
            {
                var response = await api.ListAsync(cancellationToken);
                _catalog = response.Items.ToArray();
                return _catalog;
            }
            catch (ApiException e) when (e.StatusCode is HttpStatusCode.NotFound)
            {
                _catalog = [];
                return _catalog;
            }
            catch (ApiException e) when (e.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return [];
            }
        }
        finally
        {
            _catalogLock.Release();
        }
    }
}
