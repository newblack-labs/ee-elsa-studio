using Elsa.Studio.Contracts;
using Elsa.Studio.Extensions;
using Elsa.Studio.Models;
using MudBlazor;

namespace Elsa.Studio.Diagnostics.StructuredLogs.Menu;

/// <summary>
/// Exposes menu entries for structured logs.
/// </summary>
/// <remarks>
/// The remote-feature check is what its sibling diagnostics menus (console logs, OpenTelemetry) and
/// the alterations menu already do, and without it this entry appears on hosts that never enabled
/// structured logging. Structured logs need a store, so a host that installs the module without a
/// persistence provider — or that runs a provider Elsa has no store for yet — links to a page whose
/// only content is "The live structured log subscription is disconnected".
/// </remarks>
public class StructuredLogsMenu(IRemoteFeatureProvider remoteFeatureProvider) : IMenuProvider
{
    /// <inheritdoc />
    public async ValueTask<IEnumerable<MenuItem>> GetMenuItemsAsync(CancellationToken cancellationToken = default)
    {
        if (!await remoteFeatureProvider.IsEnabledOrDefaultAsync(Feature.RemoteFeatureName, cancellationToken))
            return [];

        var menuItems = new List<MenuItem>
        {
            new()
            {
                Icon = Icons.Material.Filled.FormatListBulleted,
                Href = "diagnostics/structured-logs",
                Text = "Structured Logs",
                GroupName = MenuItemGroups.Diagnostics.Name
            }
        };

        return menuItems;
    }
}
