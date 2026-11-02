using Flock.Kit;

namespace Flock.Tray.Tests;

public class WorktreeNotifierTests
{
    private static Status.Site Worktree(string name, string? state) => new()
    {
        Name = name,
        Parent = "acme",
        Path = $"C:\\{name}",
        Worktree = state is null ? null : new Status.WorktreeSetup { State = state },
    };

    private static Status With(params Status.Site[] sites) => new() { Sites = [new Status.Site { Name = "acme", Path = "C:\\acme" }, .. sites] };

    [Fact]
    public void NotifiesOfASetupThatFailedSinceTheLastStatus()
    {
        var before = With(Worktree("a.acme", Status.WorktreeState.Running), Worktree("b.acme", Status.WorktreeState.Failed));
        var now = With(Worktree("a.acme", Status.WorktreeState.Failed), Worktree("b.acme", Status.WorktreeState.Failed));

        Assert.Equal(["a.acme"], WorktreeNotifier.NewlyFailed(before, now, new HashSet<string>()).Select(s => s.Name));
    }

    [Fact]
    public void NotifiesOfAWorktreeThatFailedBeforeAnyStatusShowedIt()
    {
        var now = With(Worktree("oops.acme", Status.WorktreeState.Failed));

        Assert.Equal(["oops.acme"], WorktreeNotifier.NewlyFailed(With(), now, new HashSet<string>()).Select(s => s.Name));
    }

    [Fact]
    public void LeavesAWorktreeThatADialogFollows()
    {
        var now = With(Worktree("oops.acme", Status.WorktreeState.Failed));

        Assert.Empty(WorktreeNotifier.NewlyFailed(With(), now, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "c:\\OOPS.acme" }));
    }
}
