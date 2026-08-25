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

        return RemoteFeatureNames.Matches(featureName, catalog.Select(feature => feature.FullName));
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
                // Deliberately not cached, unlike an earlier version of this catch. A 404 here is as
                // likely to be transient as permanent — the backend still starting, or a stale proxy
                // route — and caching the empty result hides every [RemoteFeature]-gated menu for the
                // rest of the session, with no error anywhere, since the gate fails closed.
                return [];
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
