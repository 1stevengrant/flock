using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Flock.Tray;

/// <summary>Opens the app's windows for Screenshots, the way the tray menu and links do.</summary>
public sealed partial class App
{
    internal Task<Window> ShowWindow(WindowKind kind, SettingsPane? pane = null)
    {
        Open(kind, pane);
        return Task.FromResult(windows[kind]);
    }

    internal Task<Window> ShowSettings(SettingsPane pane) => ShowWindow(WindowKind.Settings, pane);

    /// <summary>A Settings pane scrolled so that a heading (a TextBlock with that text) is at the top.</summary>
    internal async Task<Window> ShowSettings(SettingsPane pane, string heading)
    {
        var window = await ShowSettings(pane);
        await Settled();
        Find<TextBlock>(window.Content, t => t.Text == heading)?.StartBringIntoView(new BringIntoViewOptions
        {
            AnimationDesired = false,
            VerticalAlignmentRatio = 0,
            VerticalOffset = -16,
        });
        return window;
    }

    internal async Task<Window> ShowSite(string name)
    {
        var window = (SettingsWindow)await ShowSettings(SettingsPane.Sites);
        await Settled();
        window.Sites.SelectForScreenshot(name);
        await Settled();
        return window;
    }

    /// <summary>The Add Domain dialog of a site, with a domain typed in.</summary>
    internal async Task<Window> ShowAddDomain(string site, string domain)
    {
        var window = (SettingsWindow)await ShowSite(site);
        _ = window.Sites.AddDomainForScreenshot(site);
        // The dialog's popup opens, then its content loads.
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(100);
            var box = VisualTreeHelper.GetOpenPopupsForXamlRoot(window.XamlRoot)
                .Select(popup => popup.Child is { } child ? Find<TextBox>(child, _ => true) : null)
                .FirstOrDefault(found => found is not null);
            if (box is not null)
            {
                box.Text = domain;
                box.Select(domain.Length, 0);
                break;
            }
        }
        return window;
    }

    /// <summary>A site's "…" menu, open.</summary>
    internal async Task<Window> ShowSiteMenu(string site)
    {
        var window = await ShowSite(site);
        if (Find<Button>(window.Content, b => b.Flyout is MenuFlyout) is { } more)
        {
            more.Flyout.ShowAt(more);
        }
        return window;
    }

    internal async Task<Window> ShowService(string id)
    {
        var window = await ShowSettings(SettingsPane.Services);
        ((SettingsWindow)window).ShowService(id);
        return window;
    }

    internal async Task<Window> ShowMail()
    {
        var window = await ShowWindow(WindowKind.Mail);
        await Settled();
        ((MailWindow)window).SelectFirst();
        return window;
    }

    internal async Task<Window> ShowLog(string site)
    {
        var window = await ShowWindow(WindowKind.Logs);
        await ((LogsWindow)window).ShowSource(new LogStore.SiteSource(site));
        return window;
    }

    internal Task<Window> ShowPanelForScreenshot()
    {
        ShowPanel();
        return Task.FromResult<Window>(panel!);
    }

    internal void CloseScreenshotWindows()
    {
        foreach (var window in windows.Values.ToList())
        {
            window.Close();
        }
        panel?.Hide();
    }

    /// <summary>Time for a window's first loads from the fake daemon and its layout.</summary>
    private static Task Settled() => Task.Delay(TimeSpan.FromMilliseconds(600));

    /// <summary>The first element of a type in the visual tree that matches.</summary>
    private static T? Find<T>(DependencyObject root, Func<T, bool> matches) where T : DependencyObject
    {
        if (root is T element && matches(element))
        {
            return element;
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (Find(VisualTreeHelper.GetChild(root, i), matches) is { } found)
            {
                return found;
            }
        }
        return null;
    }
}

internal sealed partial class MailWindow
{
    internal void SelectFirst() => list.SelectedIndex = 0;
}

internal sealed partial class SettingsWindow
{
    internal SitesPane Sites => (SitesPane)panes[SettingsPane.Sites];
}

internal sealed partial class SitesPane
{
    internal void SelectForScreenshot(string name) => Select(name);

    internal Task AddDomainForScreenshot(string name) => AddDomain(Model.Status!.Sites.First(s => s.Name == name));
}
