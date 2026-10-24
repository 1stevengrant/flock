using Microsoft.UI.Xaml;

namespace Flock.Tray;

/// <summary>Startup and presentation settings for an alternate application host.</summary>
internal sealed class AppOptions
{
    public ApplicationTheme? Theme { get; init; }
    public bool UseSolidBackground { get; init; }
    public bool KeepPanelOpen { get; init; }
    public bool EnableBackgroundServices { get; init; } = true;
    public string? PreferencesPath { get; init; }
    public Func<App, Task>? OnStarted { get; init; }
}
