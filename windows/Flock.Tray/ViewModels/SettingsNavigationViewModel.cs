using Flock.Tray.Settings;
namespace Flock.Tray.ViewModels;

/// <summary>Owns one cancellable activation per selected settings pane.</summary>
public sealed class SettingsNavigationViewModel : IAsyncDisposable
{
    private readonly Func<SettingsPane, CancellationToken, Task> activate;
    private readonly object gate = new();
    private CancellationTokenSource? currentCancellation;
    private Task? currentTask;

    public SettingsNavigationViewModel(Func<SettingsPane, CancellationToken, Task> activate)
    {
        this.activate = activate;
    }

    public SettingsPane? Selected { get; private set; }

    public async ValueTask DisposeAsync()
    {
        Task? task;
        lock (gate)
        {
            currentCancellation?.Cancel();
            task = currentTask;
        }
        if (task is not null)
        {
            await task;
        }
    }

    public Task ActivateAsync(SettingsPane pane)
    {
        lock (gate)
        {
            if (Selected == pane)
            {
                return currentTask ?? Task.CompletedTask;
            }
            currentCancellation?.Cancel();
            var old = currentTask;
            var cancellation = new CancellationTokenSource();
            currentCancellation = cancellation;
            Selected = pane;
            currentTask = ActivateCoreAsync(old, pane, cancellation);
            return currentTask;
        }
    }

    public Task RetryAsync()
    {
        lock (gate)
        {
            if (Selected is not { } pane)
            {
                return Task.CompletedTask;
            }
            Selected = null;
            return ActivateAsync(pane);
        }
    }

    private async Task ActivateCoreAsync(Task? old, SettingsPane pane, CancellationTokenSource cancellation)
    {
        if (old is not null && !old.IsCompleted)
        {
            try
            {
                await old;
            }
            catch (OperationCanceledException)
            {
            }
        }
        try
        {
            await activate(pane, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            lock (gate)
            {
                if (ReferenceEquals(currentCancellation, cancellation))
                {
                    currentCancellation = null;
                }
            }
            cancellation.Dispose();
        }
    }
}

/// <summary>Window-level settings state shared by the retained navigation surface.</summary>
public sealed class SettingsWindowViewModel : IAsyncDisposable
{
    public SettingsWindowViewModel(Func<SettingsPane, CancellationToken, Task> activate)
    {
        Navigation = new SettingsNavigationViewModel(activate);
    }

    public SettingsNavigationViewModel Navigation { get; }

    public ValueTask DisposeAsync()
    {
        return Navigation.DisposeAsync();
    }
}