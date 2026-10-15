using M800.App.Pages;
using M800.App.ViewModels;
using Wpf.Ui.Abstractions;

namespace M800.App.Navigation;

/// <summary>Cria cada página uma única vez (as páginas ficam em cache).</summary>
public sealed class PageProvider(AppHost host) : INavigationViewPageProvider
{
    private readonly Dictionary<Type, object> _cache = [];

    private readonly Dictionary<Type, Func<object>> _factories = new()
    {
        [typeof(HomePage)] = () => new HomePage(new HomeViewModel(host)),
        [typeof(DpiPage)] = () => new DpiPage(new DpiViewModel(host)),
        [typeof(LightingPage)] = () => new LightingPage(new LightingViewModel(host)),
        [typeof(PerformancePage)] = () => new PerformancePage(new PerformanceViewModel(host)),
        [typeof(PowerPage)] = () => new PowerPage(new PowerViewModel(host)),
        [typeof(ButtonsPage)] = () => new ButtonsPage(new ButtonsViewModel(host)),
        [typeof(MacrosPage)] = () => new MacrosPage(new MacrosViewModel(host)),
        [typeof(ProfilesPage)] = () => new ProfilesPage(new ProfilesViewModel(host)),
        [typeof(SettingsPage)] = () => new SettingsPage(new SettingsViewModel(host)),
        [typeof(PointerPage)] = () => new PointerPage(new PointerViewModel(host)),
        [typeof(TestsPage)] = () => new TestsPage(new TestsViewModel(host)),
        // PAGES
    };

    public object? GetPage(Type pageType)
    {
        if (!_cache.TryGetValue(pageType, out var page))
        {
            page = _factories[pageType]();
            _cache[pageType] = page;
        }
        return page;
    }
}
