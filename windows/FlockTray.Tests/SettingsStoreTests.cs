using System.Net;
using System.Text.Json;
using Flock.Kit;
using Flock.Tray;

namespace Flock.Tray.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string dir = Directory.CreateTempSubdirectory("flock-settings-").FullName;
    private readonly Handler handler = new();
    private readonly FlockClient client;
    private readonly SettingsStore store;
    private int changes;

    public SettingsStoreTests()
    {
        var path = Path.Combine(dir, "api.json");
        File.WriteAllText(path, """{"url":"http://127.0.0.1:1","token":"test"}""");
        client = new FlockClient(path, handler);
        store = new SettingsStore(client) { OnChange = OnChange };
    }

    private Task OnChange()
    {
        changes++;
        // Model status updates also refresh the visible Settings window.
        return store.RefreshAsync();
    }

    public void Dispose()
    {
        client.Dispose();
        Directory.Delete(dir, recursive: true);
    }

    private sealed class Handler : HttpMessageHandler
    {
        public bool FailReads { get; set; }
        public Dictionary<string, HttpStatusCode> Failures { get; } = [];
        public Dictionary<string, string> ErrorMessages { get; } = [];
        public Func<HttpRequestMessage, Task>? BeforeRespond { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (BeforeRespond is not null)
            {
                await BeforeRespond(request);
            }
            var path = request.RequestUri!.AbsolutePath;
            var status = FailReads ? HttpStatusCode.ServiceUnavailable
                : Failures.GetValueOrDefault(path, HttpStatusCode.OK);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(status != HttpStatusCode.OK
                    ? JsonSerializer.Serialize(new { error = ErrorMessages.GetValueOrDefault(path, "refresh failed") })
                    : path == "/v1/services" ? "[]" : "{}"),
            };
        }
    }

    public enum ActionKind { Site, Services, Php, FrankenPhp, Mcp, Node, Catalog, Postgres }

    private Task Perform(ActionKind kind, bool fail, Task? pending = null) => kind switch
    {
        ActionKind.Site => store.PerformAsync(_ => Result(new object(), fail, pending)),
        ActionKind.Services => store.PerformOnServicesAsync(_ => Result(new List<Service>(), fail, pending)),
        ActionKind.Php => store.PerformOnPhpAsync(_ => Result(new PhpCatalog(), fail, pending)),
        ActionKind.FrankenPhp => store.PerformOnFrankenPhpAsync(_ => Result(new FrankenPhpState(), fail, pending)),
        ActionKind.Mcp => store.PerformOnMcpAsync(_ => Result(new McpState(), fail, pending)),
        ActionKind.Node => store.PerformOnNodeAsync(_ => Result(new NodeCatalog(), fail, pending)),
        ActionKind.Catalog => store.PerformOnCatalogAsync(_ => Result(new ServiceCatalog(), fail, pending)),
        ActionKind.Postgres => store.PerformOnPostgresAsync(_ => Result(new PostgresVersions(), fail, pending)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static async Task<T> Result<T>(T value, bool fail, Task? pending)
    {
        if (pending is not null)
        {
            await pending;
        }
        if (fail)
        {
            throw new FlockException("action failed");
        }
        return value;
    }

    [Theory]
    [InlineData(ActionKind.Site)]
    [InlineData(ActionKind.Services)]
    [InlineData(ActionKind.Php)]
    [InlineData(ActionKind.FrankenPhp)]
    [InlineData(ActionKind.Mcp)]
    [InlineData(ActionKind.Node)]
    [InlineData(ActionKind.Catalog)]
    [InlineData(ActionKind.Postgres)]
    public async Task FailedActionsSurviveRefreshesUntilASuccessfulAction(ActionKind kind)
    {
        await Perform(kind, fail: true);
        Assert.Equal("action failed", store.Error);
        Assert.False(store.IsBusy);

        await store.RefreshAsync();
        await store.RefreshAsync();
        Assert.Equal("action failed", store.Error);
        Assert.NotNull(store.Config);

        await Perform(kind, fail: false);
        Assert.Null(store.Error);
    }

    [Fact]
    public async Task RefreshFailuresDoNotReplaceActionFailures()
    {
        handler.FailReads = true;
        await Perform(ActionKind.Site, fail: true);
        Assert.Equal("action failed", store.Error);
        Assert.Equal(1, changes);

        handler.FailReads = false;
        await store.RefreshAsync();
        Assert.Equal("action failed", store.Error);

        store.DismissError();
        Assert.Null(store.Error);
    }

    [Fact]
    public async Task SuccessfulActionDoesNotClearARefreshFailure()
    {
        handler.FailReads = true;
        await Perform(ActionKind.Site, fail: true);
        await Perform(ActionKind.Site, fail: false);

        Assert.Equal("refresh failed", store.Error);
    }

    [Fact]
    public async Task SuccessfulRefreshClearsReadErrorsAndNotifiesEvenWhenDataIsUnchanged()
    {
        await store.RefreshAsync();
        handler.Failures["/v1/config"] = HttpStatusCode.ServiceUnavailable;
        await store.RefreshAsync();
        Assert.Equal("refresh failed", store.Error);

        var notifications = 0;
        store.Changed += () => notifications++;
        handler.Failures.Clear();
        await store.RefreshAsync();
        Assert.Null(store.Error);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task UnchangedRefreshDoesNotNotifyForAPersistentActionError()
    {
        await store.RefreshAsync();
        store.ShowError("local action failed");
        var notifications = 0;
        store.Changed += () => notifications++;

        await store.RefreshAsync();
        Assert.Equal("local action failed", store.Error);
        Assert.Equal(0, notifications);

        store.DismissError();
        Assert.Null(store.Error);
        Assert.Equal(1, notifications);
    }

    public enum ReadKind { Config, Services, Php, FrankenPhp, Mcp, Node, Catalog, Postgres }

    private static string PathFor(ReadKind kind) => kind switch
    {
        ReadKind.Config => "/v1/config",
        ReadKind.Services => "/v1/services",
        ReadKind.Php => "/v1/php/versions",
        ReadKind.FrankenPhp => "/v1/frankenphp",
        ReadKind.Mcp => "/v1/mcp",
        ReadKind.Node => "/v1/node/versions",
        ReadKind.Catalog => "/v1/catalog",
        ReadKind.Postgres => "/v1/postgres/versions",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private Task Refresh(ReadKind kind) => kind switch
    {
        ReadKind.Config or ReadKind.Services => store.RefreshAsync(),
        ReadKind.Php => store.RefreshPhpAsync(),
        ReadKind.FrankenPhp => store.RefreshFrankenPhpAsync(),
        ReadKind.Mcp => store.RefreshMcpAsync(),
        ReadKind.Node => store.RefreshNodeAsync(),
        ReadKind.Catalog => store.RefreshCatalogAsync(),
        ReadKind.Postgres => store.RefreshPostgresAsync(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    [Theory]
    [InlineData(ReadKind.Config)]
    [InlineData(ReadKind.Services)]
    [InlineData(ReadKind.Php)]
    [InlineData(ReadKind.FrankenPhp)]
    [InlineData(ReadKind.Mcp)]
    [InlineData(ReadKind.Node)]
    [InlineData(ReadKind.Catalog)]
    [InlineData(ReadKind.Postgres)]
    public async Task EachReadClearsOnlyItsOwnFailureEvenWhenDataIsUnchanged(ReadKind kind)
    {
        await Refresh(kind);
        handler.Failures[PathFor(kind)] = HttpStatusCode.ServiceUnavailable;
        await Refresh(kind);
        Assert.Equal("refresh failed", store.Error);

        handler.Failures.Clear();
        await Refresh(kind is ReadKind.Config or ReadKind.Services ? ReadKind.Php : ReadKind.Config);
        Assert.Equal("refresh failed", store.Error);

        var notifications = 0;
        store.Changed += () => notifications++;
        await Refresh(kind);
        Assert.Null(store.Error);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task ConfigFailureDoesNotPreventServicesFromUpdating()
    {
        handler.Failures["/v1/config"] = HttpStatusCode.ServiceUnavailable;
        await store.RefreshAsync();
        Assert.Equal("refresh failed", store.Error);

        var notifications = 0;
        store.Changed += () => notifications++;
        await store.PerformOnServicesAsync(_ => Task.FromResult(new List<Service> { new() }));
        Assert.Empty(store.Services);
        Assert.True(notifications > 0);
        Assert.Equal("refresh failed", store.Error);
    }

    [Fact]
    public async Task ConcurrentReadFailuresRecoverIndependently()
    {
        handler.Failures["/v1/php/versions"] = HttpStatusCode.ServiceUnavailable;
        handler.Failures["/v1/node/versions"] = HttpStatusCode.ServiceUnavailable;
        await Task.WhenAll(store.RefreshPhpAsync(), store.RefreshNodeAsync());
        Assert.Equal("refresh failed", store.Error);

        handler.Failures.Clear();
        await store.RefreshPhpAsync();
        Assert.Equal("refresh failed", store.Error);
        await store.RefreshNodeAsync();
        Assert.Null(store.Error);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.NotImplemented)]
    public async Task UnsupportedFrankenPhpClearsOnlyItsOwnFailure(HttpStatusCode status)
    {
        await store.RefreshFrankenPhpAsync();
        Assert.NotNull(store.FrankenPhp);
        handler.Failures["/v1/frankenphp"] = HttpStatusCode.ServiceUnavailable;
        await store.RefreshFrankenPhpAsync();
        Assert.Equal("refresh failed", store.Error);

        handler.Failures["/v1/frankenphp"] = status;
        await store.RefreshFrankenPhpAsync();
        Assert.Null(store.FrankenPhp);
        Assert.Null(store.Error);

        handler.Failures["/v1/php/versions"] = HttpStatusCode.ServiceUnavailable;
        await store.RefreshPhpAsync();
        await store.RefreshFrankenPhpAsync();
        Assert.Equal("refresh failed", store.Error);
    }

    [Fact]
    public async Task InFlightReadCannotClearANewerActionFailure()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.BeforeRespond = async _ =>
        {
            started.TrySetResult();
            await finish.Task;
        };
        var refresh = store.RefreshAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        store.ShowError("action failed during refresh");
        finish.SetResult();
        await refresh;
        Assert.Equal("action failed during refresh", store.Error);
    }

    [Fact]
    public async Task DismissalClearsAllCurrentFailuresButNotFutureFailures()
    {
        handler.FailReads = true;
        await Task.WhenAll(store.RefreshPhpAsync(), store.RefreshNodeAsync());
        store.ShowError("local action failed");
        store.DismissError();
        Assert.Null(store.Error);

        await store.RefreshPhpAsync();
        Assert.Equal("refresh failed", store.Error);
    }

    [Fact]
    public async Task ReadFailuresHaveStablePriorityAndRemainAfterAnActionSucceeds()
    {
        handler.Failures["/v1/node/versions"] = HttpStatusCode.ServiceUnavailable;
        handler.ErrorMessages["/v1/node/versions"] = "Node read failed";
        handler.Failures["/v1/php/versions"] = HttpStatusCode.ServiceUnavailable;
        handler.ErrorMessages["/v1/php/versions"] = "PHP read failed";
        await store.RefreshNodeAsync();
        await store.RefreshPhpAsync();
        Assert.Equal("PHP read failed", store.Error);

        await Perform(ActionKind.Site, fail: true);
        Assert.Equal("action failed", store.Error);
        await Perform(ActionKind.Site, fail: false);
        Assert.Equal("PHP read failed", store.Error);

        handler.Failures.Clear();
        await store.RefreshPhpAsync();
        Assert.Equal("Node read failed", store.Error);
        await store.RefreshNodeAsync();
        Assert.Null(store.Error);
    }

    [Fact]
    public async Task ServiceActionReturnsItsOwnOutcomeNotTheDisplayedError()
    {
        handler.FailReads = true;
        Assert.True(await store.PerformOnServicesAsync(_ => Task.FromResult(new List<Service>())));
        Assert.Equal("refresh failed", store.Error);

        handler.FailReads = false;
        Assert.False(await store.PerformOnServicesAsync(_ => Task.FromException<List<Service>>(new FlockException("action failed"))));
        Assert.Equal("action failed", store.Error);
    }

    [Theory]
    [InlineData(ActionKind.Site, true)]
    [InlineData(ActionKind.Services, true)]
    [InlineData(ActionKind.Php, true)]
    [InlineData(ActionKind.Mcp, true)]
    [InlineData(ActionKind.FrankenPhp, false)]
    [InlineData(ActionKind.Node, false)]
    [InlineData(ActionKind.Catalog, false)]
    [InlineData(ActionKind.Postgres, false)]
    public async Task ActionsKeepTheirBusyBehaviorWhilePending(ActionKind kind, bool busy)
    {
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var action = Perform(kind, fail: false, pending: finish.Task);
        Assert.Equal(busy, store.IsBusy);
        Assert.False(action.IsCompleted);

        finish.SetResult();
        await action;
        Assert.False(store.IsBusy);
    }

    [Fact]
    public async Task RepeatedUnchangedReadFailuresDoNotNotify()
    {
        handler.FailReads = true;
        await store.RefreshAsync();
        var notifications = 0;
        store.Changed += () => notifications++;

        await store.RefreshAsync();
        Assert.Equal("refresh failed", store.Error);
        Assert.Equal(0, notifications);
    }
}
