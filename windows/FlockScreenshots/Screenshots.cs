using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;

namespace Flock.Tray;

/// <summary>
/// The shots for the website's docs: each opens a window on the demo data, waits for it to settle
/// and saves it as {id}-windows-{theme}.png. The ids are the docs' screenshot:… placeholders.
/// </summary>
internal sealed class Screenshots(Screenshots.Options options)
{
    public sealed record Options(string Out, string? Theme, HashSet<string>? Only)
    {
        /// <summary>Null when the arguments make no sense.</summary>
        public static Options? Parse(string[] args)
        {
            string? output = null, theme = null;
            HashSet<string>? only = null;
            for (var i = 0; i + 1 < args.Length; i += 2)
            {
                switch (args[i])
                {
                    case "--out": output = Path.GetFullPath(args[i + 1]); break;
                    case "--theme" when args[i + 1] is "light" or "dark": theme = args[i + 1]; break;
                    case "--only": only = [.. args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]; break;
                    default: return null;
                }
            }
            return output is null || args.Length % 2 != 0 ? null : new Options(output, theme, only);
        }
    }

    /// <summary>One screenshot and how to show it.</summary>
    private sealed record Shot(string Id, Func<App, Task<Window>> Show);

    private static readonly Shot[] All =
    [
        new("settings-general-project-folders", app => app.ShowSettings(SettingsPane.General, "Updates")),
        new("settings-sites", app => app.ShowSite("shop")),
        new("settings-sites-https", app => app.ShowSite("acme")),
        new("settings-sites-add-domain", app => app.ShowAddDomain("acme", "admin")),
        new("settings-sites-pin", app => app.ShowSiteMenu("shop")),
        new("settings-php", app => app.ShowSettings(SettingsPane.Php)),
        new("settings-node", app => app.ShowSettings(SettingsPane.Node)),
        new("settings-services-postgres", app => app.ShowService("postgres")),
        new("settings-services-install", app => app.ShowService("mariadb")),
        new("settings-debugger", app => app.ShowSettings(SettingsPane.Debugger)),
        new("logs-window", app => app.ShowLog("acme")),
        new("mail-window", app => app.ShowMail()),
        new("dumps-window", app => app.ShowWindow(WindowKind.Dumps)),
        new("panel-running", app => app.ShowPanelForScreenshot()),
    ];

    /// <summary>Set by Program before the app starts.</summary>
    public static Screenshots Current { get; set; } = null!;

    /// <summary>The number of shots that failed: the process's exit code.</summary>
    public int Failures { get; private set; }

    public ApplicationTheme Theme => options.Theme == "dark" ? ApplicationTheme.Dark : ApplicationTheme.Light;

    public CoreWebView2PreferredColorScheme WebViewColorScheme => options.Theme == "dark"
        ? CoreWebView2PreferredColorScheme.Dark
        : CoreWebView2PreferredColorScheme.Light;

    /// <summary>
    /// Covers the windows' Mica or Acrylic, whose look depends on the wallpaper, with their solid
    /// fallback. The backdrop itself stays: without one, the windows show up black.
    /// </summary>
    public static Brush WindowBackground => (Brush)Application.Current.Resources["SolidBackgroundFillColorBaseBrush"];

    /// <summary>Takes over from App.OnLaunched: no tray icon, no polling, no updates.</summary>
    public bool Start(App app)
    {
        _ = RunAsync(app);
        return true;
    }

    private async Task RunAsync(App app)
    {
        foreach (var shot in All.Where(s => options.Only?.Contains(s.Id) ?? true))
        {
            var file = Path.Combine(options.Out, $"{shot.Id}-windows-{options.Theme}.png");
            try
            {
                await app.Model.RefreshAsync();
                var window = await shot.Show(app);
                var hwnd = Microsoft.UI.Win32Interop.GetWindowFromWindowId(window.AppWindow.Id);
                Capture.Prepare(hwnd);
                // Panes load their data, lists lay out, WebView2 navigates.
                await Task.Delay(TimeSpan.FromSeconds(2));
                if (!window.AppWindow.IsVisible)
                {
                    // The screen would show whatever is behind it.
                    throw new InvalidOperationException("the window closed before it was captured");
                }
                await Capture.SaveAsync(hwnd, Math.Max(1, Native.GetDpiForWindow(hwnd) / 96.0), file);
                Console.WriteLine(file);
            }
            catch (Exception e)
            {
                Failures++;
                Console.Error.WriteLine($"{shot.Id} ({options.Theme}) failed: {e}");
            }
            finally
            {
                app.CloseScreenshotWindows();
            }
        }
        app.Exit();
    }
}
