using System.Diagnostics.CodeAnalysis;
using Elsa.Studio.Contracts;
using Elsa.Studio.Extensions;
using Elsa.Studio.Models;
using Microsoft.AspNetCore.Components;

namespace Elsa.Studio.Services;

/// <inheritdoc />
public class DefaultAppBarService : IAppBarService
{
    private readonly ICollection<AppBarElement> _elements = new List<AppBarElement>();
    private readonly HashSet<Type> _componentTypes = [];

    /// <inheritdoc />
    public event Action? AppBarItemsChanged;

    /// <inheritdoc />
    public IEnumerable<AppBarElement> AppBarElements => _elements.OrderBy(x => x.Order).ToList();

    /// <inheritdoc />
    public IEnumerable<RenderFragment> AppBarComponents => AppBarElements.Select(x => x.Component).ToList();

    /// <inheritdoc />
    public void AddAppBarItem<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>() where T : IComponent
    {
        AddComponent<T>();
    }

    /// <inheritdoc />
    public void AddComponent<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(float? order = null) where T : IComponent
    {
        // Guard on the component type. AddElement's own duplicate check cannot catch these: every call
        // here builds a fresh AppBarElement holding a fresh render-fragment delegate, so no two
        // elements are ever equal. MainLayout adds DarkModeToggle and ProductInfo from OnInitialized,
        // and the layout re-initializes whenever the authentication state changes without a full page
        // load — which is what brokered sign-in does — so every sign-in appended another copy of both
        // icons to the app bar until the next hard refresh.
        if (!_componentTypes.Add(typeof(T)))
            return;

        var element = new AppBarElement
        {
            Order = order ?? 0,
            Component = builder => builder.CreateComponent<T>()
        };

        AddElement(element);
    }

    /// <inheritdoc />
    public void AddElement<T>(float? order = null) where T : AppBarElement, new()
    {
        var element = new T();

        if (order.HasValue)
            element.Order = order.Value;

        AddElement(element);
    }

    /// <inheritdoc />
    public void AddElement(AppBarElement element)
    {
        if (_elements.Contains(element))
            return;

        _elements.Add(element);
        AppBarItemsChanged?.Invoke();
    }
}
