using Flock.Kit;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Flock.Tray;

/// <summary>
/// Node.js majors from the package manifest, like the PHP pane: install, update, make default and uninstall
/// them. Exact versions a project pins (.nvmrc) are offered by `node` in the terminal instead.
/// </summary>
internal sealed class NodePane(SettingsWindow window, AppModel model) : Pane(window, model)
{
    // Majors with an install or uninstall in flight (and what to show meanwhile); the daemon answers when it's done.
    private readonly Dictionary<string, string> busy = [];
    private bool checking;
    // The major the first run installs as the default, as last seen in the status (see Build).
    private string? installingDefault;

    public override string Title => "Node";
    public override string Subtitle => "node, npm and npx follow the project's .nvmrc / .node-version, then the site's version, then the default.";

    public override void Shown(CancellationToken hidden) => _ = Store.RefreshNodeAsync();

    /// <summary>
    /// The major the daemon installs as the default on its first run. The Node.js versions don't
    /// show that install, only the status does; its row shows it like an install started here.
    /// </summary>
    private string? DefaultInstalling =>
        Model.Status?.Defaults?.Node is { IsInstalling: true, Version.Length: > 0 } node ? node.Version : null;

    /// <summary>What a major's row shows while a request or the default's install runs; null when nothing does.</summary>
    private string? Step(NodeRelease release) =>
        busy.TryGetValue(release.Major, out var step) ? step
        : release.Major == DefaultInstalling && !release.IsInstalled ? $"Downloading Node.js {release.Latest}…"
        : null;

    /// <summary>
    /// Asks about a pending flock://install/node link (App.OpenLink) once the versions are loaded,
    /// like Services does for services.
    /// </summary>
    private void ConfirmPendingLink()
    {
        List<string> installing = DefaultInstalling is { } byDefault ? [.. busy.Keys, byDefault] : [.. busy.Keys];
        if (Model.PendingInstall is not { Service: InstallLink.Node } link || link.ResolveNode(Store.Node, installing) is not { } request)
        {
            return;
        }
        Model.PendingInstall = null;
        // Not while building the pane: a dialog needs the window's content in place.
        Window.DispatcherQueue.TryEnqueue(async () =>
        {
            if (request.Target is not { } target)
            {
                await Ui.Inform(Window.Content.XamlRoot, request.Title, request.Message);
                return;
            }
            if (await Window.Confirm(request.Title, request.Message, "Install"))
            {
                var major = target.Line;
                await Run(major, $"Downloading Node.js {target.Version}…", c => c.InstallNodeMajorAsync(major));
            }
        });
    }

    public override UIElement Build()
    {
        // The default's install is done (or failed): the versions show the result.
        var installing = DefaultInstalling;
        if (installingDefault is not null && installing != installingDefault)
        {
            _ = Store.RefreshNodeAsync();
        }
        installingDefault = installing;

        if (Store.Node is not { } catalog)
        {
            return Ui.Row(10, new ProgressRing { Width = 16, Height = 16 }, Ui.Text("Loading Node.js versions…", foreground: Ui.SecondaryText));
        }
        ConfirmPendingLink();

        var checkedText = catalog.CheckedAt is { } at ? $"Checked {Ui.Relative(at)}" : "Not checked yet";
        UIElement check = checking
            ? new ProgressRing { Width = 14, Height = 14 }
            : Ui.Link("Check now", async () =>
            {
                checking = true;
                Rerender();
                await Store.PerformOnNodeAsync(c => c.CheckNodeReleasesAsync());
                checking = false;
                Rerender();
            });
        var header = Ui.Spread(Ui.Text("Versions", "BodyStrongTextBlockStyle"), Ui.Row(6, Ui.Caption(checkedText, wrap: false), check));
        header.Margin = new Thickness(1, 0, 0, 6);

        var versions = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 24) };
        versions.Children.Add(header);
        foreach (var release in catalog.Versions)
        {
            versions.Children.Add(Row(release));
        }
        if (catalog.Versions.Count == 0)
        {
            versions.Children.Add(Ui.Card(Ui.Text("No Node.js versions to show yet.", foreground: Ui.SecondaryText)));
        }
        if (catalog.CheckError.Length > 0)
        {
            versions.Children.Add(Ui.Row(6, Ui.Icon(Ui.GlyphWarning, 12, Ui.Caution), Ui.Caption($"Couldn't check for updates: {catalog.CheckError}", Ui.Caution)));
        }
        versions.Children.Add(GeneralPane.Footnote("Official Node.js builds, the newest release of each major. A project that pins a version in .nvmrc gets that major offered when you run node, npm or npx there. "
            + "Global packages (npm install -g) belong to a Node line and survive its updates; those of the default version are on your PATH."));
        return versions;
    }

    private Border Row(NodeRelease release)
    {
        var title = Ui.Row(8, Ui.Text($"Node {release.Major}", "BodyStrongTextBlockStyle", wrap: false), Ui.Caption(VersionText(release), wrap: false));
        if (release.IsDefault)
        {
            title.Children.Add(Ui.Badge("Default"));
        }

        var icon = Ui.Icon(Ui.GlyphTerminal, 20, release.IsInstalled ? Ui.Accent : Ui.TertiaryText);
        var info = Ui.Row(14, icon, Ui.Column(2, title, Detail(release)));
        return Ui.Card(Ui.Spread(info, Actions(release)));
    }

    private static string VersionText(NodeRelease release) =>
        release.Update ? $"{release.Installed} → {release.Latest}" : release.IsInstalled ? release.Installed : release.Latest;

    /// <summary>The status line under the name.</summary>
    private UIElement Detail(NodeRelease release)
    {
        if (Step(release) is { } step)
        {
            return Ui.Row(6, new ProgressRing { Width = 12, Height = 12 }, Ui.Caption(step, wrap: false));
        }
        var (status, color) = release switch
        {
            { Update: true } => ("Update available", Ui.Accent),
            { IsInstalled: true } => ("Up to date", Ui.SecondaryText),
            _ => ("Not installed", Ui.SecondaryText),
        };
        var line = Ui.Row(0, Ui.Caption(status, color, wrap: false));
        if (release.Npm.Length > 0)
        {
            line.Children.Add(Ui.Caption($" · npm {release.Npm}", wrap: false));
            if (release.NpmLatest.Length > 0)
            {
                line.Children.Add(Ui.Caption($" (npm {release.NpmLatest} available)", Ui.Accent, wrap: false));
            }
        }
        return line;
    }

    /// <summary>The row's buttons; disabled while its request or the default's install runs (the status line shows the progress).</summary>
    private StackPanel Actions(NodeRelease release)
    {
        var actions = BuildActions(release);
        Ui.SetEnabled(actions, Step(release) is null);
        return actions;
    }

    private StackPanel BuildActions(NodeRelease release)
    {
        var actions = Ui.Row(8);
        if (!release.IsInstalled)
        {
            actions.Children.Add(Ui.Button("Install", () => _ = Run(release.Major, $"Downloading Node.js {release.Latest}…", c => c.InstallNodeMajorAsync(release.Major)),
                glyph: Ui.GlyphDownload, tooltip: $"Downloads Node.js {release.Latest}"));
            return actions;
        }
        if (release.Update)
        {
            actions.Children.Add(Ui.Button($"Update to {release.Latest}", () => _ = Run(release.Major, $"Downloading Node.js {release.Latest}…", c => c.InstallNodeMajorAsync(release.Major)),
                accent: true, glyph: Ui.GlyphDownload, tooltip: $"Downloads Node.js {release.Latest}, then removes {release.Installed}. Global packages stay."));
        }
        if (release.NpmLatest.Length > 0)
        {
            actions.Children.Add(Ui.Button($"Update npm", () => _ = Run(release.Major, $"Updating npm to {release.NpmLatest}…", c => c.UpdateNpmAsync(release.Major)),
                glyph: Ui.GlyphDownload, tooltip: $"Installs npm {release.NpmLatest}, the newest npm that supports Node.js {release.Installed}, for Node {release.Major}"));
        }
        if (!release.IsDefault)
        {
            actions.Children.Add(Ui.Button("Make Default", () => _ = MakeDefault(release.Major)));
        }
        actions.Children.Add(More(release));
        return actions;
    }

    private Button More(NodeRelease release)
    {
        var uninstall = new MenuFlyoutItem { Text = "Uninstall…", IsEnabled = !release.IsDefault };
        uninstall.Click += async (_, _) =>
        {
            if (await Window.Confirm($"Uninstall Node {release.Major}?",
                    $"Its global npm packages are removed too. Projects and sites that use Node {release.Major} fall back to the default version until you install it again.", "Uninstall"))
            {
                await Run(release.Major, "Uninstalling…", c => c.UninstallNodeAsync(release.Major));
            }
        };
        var menu = new MenuFlyout();
        menu.Items.Add(uninstall);
        var button = Ui.IconButton(Ui.GlyphMore, "More", () => { });
        button.Flyout = menu;
        return button;
    }

    private async Task Run(string major, string step, Func<FlockClient, Task<NodeCatalog>> action)
    {
        busy[major] = step;
        Rerender();
        try
        {
            await Store.PerformOnNodeAsync(action);
        }
        finally
        {
            busy.Remove(major);
            Rerender();
        }
    }

    private async Task MakeDefault(string major)
    {
        busy[major] = "Making it the default…";
        Rerender();
        try
        {
            await Perform(c => c.UpdateSettingsAsync(new SettingsChange(DefaultNode: major)));
            await Store.RefreshNodeAsync();
        }
        finally
        {
            busy.Remove(major);
            Rerender();
        }
    }
}
