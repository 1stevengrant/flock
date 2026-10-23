using System.Net;
using Flock.Kit;
using Flock.Tray.Features.Dumps;
using Flock.Tray.Infrastructure;

namespace Flock.Tray.Tests;

public sealed class DumpsServiceTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("flock-dumps-").FullName;
    private readonly DumpsHandler handler = new();
    private readonly HttpClient http;
    private readonly FlockClient client;

    public DumpsServiceTests()
    {
        var config = Path.Combine(directory, "api.json");
        File.WriteAllText(config, "{\"url\":\"http://127.0.0.1:1\",\"token\":\"test\"}");
        http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client = new FlockClient(http, config);
    }

    public void Dispose()
    {
        client.Dispose();
        http.Dispose();
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task ClearRetiresAnOlderListAndPublishesOnlyThePostClearList()
    {
        handler.BlockFirstList = true;
        await using var service = new DumpsService(client, new OperationRunner());
        var refresh = service.RefreshAsync();
        await handler.FirstListStarted.Task;

        var clear = service.ClearAsync();
        await handler.ClearStarted.Task;
        handler.ReleaseFirstList.TrySetResult();
        await clear;
        await refresh;

        Assert.Empty(service.Dumps);
        Assert.Null(service.Error);
    }

    [Fact]
    public async Task FailedClearRetainsTheListAndItsActionErrorAfterRefresh()
    {
        handler.FailClear = true;
        await using var service = new DumpsService(client, new OperationRunner());
        await service.RefreshAsync();

        var outcome = await service.ClearAsync();

        Assert.Equal(OperationOutcome.Failed, outcome);
        Assert.Single(service.Dumps);
        Assert.Equal("clear failed", service.ActionError);
    }

    [Fact]
    public async Task MissingHeaderIsNotReportedAsARefreshFailure()
    {
        handler.MissingHeader = true;
        await using var service = new DumpsService(client, new OperationRunner());

        await service.RefreshAsync();

        Assert.True(service.HeaderMissing);
        Assert.Null(service.Header);
        Assert.Null(service.RefreshError);
    }

    [Fact]
    public async Task HeaderFailureRemainsDistinctFromAnEmptyHeader()
    {
        handler.FailHeader = true;
        await using var service = new DumpsService(client, new OperationRunner());

        await service.RefreshAsync();

        Assert.False(service.HeaderMissing);
        Assert.Null(service.Header);
        Assert.Equal("header failed", service.RefreshError);
    }

    [Fact]
    public async Task CaptureFailureSurvivesASuccessfulRead()
    {
        handler.FailCapture = true;
        await using var service = new DumpsService(client, new OperationRunner());

        var outcome = await service.SetCaptureAsync(true);
        await service.RefreshAsync();

        Assert.Equal(OperationOutcome.Failed, outcome);
        Assert.Equal("capture failed", service.ActionError);
        Assert.Single(service.Dumps);
    }

    [Fact]
    public async Task CaptureUsesOneSharedSettingsOperationKey()
    {
        handler.BlockCapture = true;
        await using var service = new DumpsService(client, new OperationRunner());

        var first = service.SetCaptureAsync(true);
        await handler.CaptureStarted.Task;

        var second = await service.SetCaptureAsync(false);

        Assert.True(service.IsCaptureBusy);
        Assert.Equal(OperationOutcome.Rejected, second);
        handler.ReleaseCapture.TrySetResult();
        Assert.Equal(OperationOutcome.Succeeded, await first);
    }

    private sealed class DumpsHandler : HttpMessageHandler
    {
        public TaskCompletionSource FirstListStarted { get; } = Signal();
        public TaskCompletionSource ReleaseFirstList { get; } = Signal();
        public TaskCompletionSource ClearStarted { get; } = Signal();
        public TaskCompletionSource CaptureStarted { get; } = Signal();
        public TaskCompletionSource ReleaseCapture { get; } = Signal();
        public bool BlockFirstList { get; set; }
        public bool FailClear { get; set; }
        public bool MissingHeader { get; set; }
        public bool FailHeader { get; set; }
        public bool FailCapture { get; set; }
        public bool BlockCapture { get; set; }
        private int listReads;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/v1/dumps" && request.Method == HttpMethod.Get)
            {
                if (Interlocked.Increment(ref listReads) == 1)
                {
                    FirstListStarted.TrySetResult();
                    if (BlockFirstList)
                    {
                        await ReleaseFirstList.Task;
                    }
                    return Json("[{\"id\":\"old\",\"group\":\"g\",\"text\":\"old\",\"html\":\"old\"}]");
                }
                return FailClear ? Json("[{\"id\":\"old\",\"group\":\"g\",\"text\":\"old\",\"html\":\"old\"}]") : Json("[]");
            }
            if (path == "/v1/dumps" && request.Method == HttpMethod.Delete)
            {
                ClearStarted.TrySetResult();
                return FailClear ? Json("{\"error\":\"clear failed\"}", HttpStatusCode.BadRequest) : Json("{}");
            }
            if (path == "/v1/dumps/header")
            {
                if (MissingHeader)
                {
                    return Json("{\"error\":\"missing\"}", HttpStatusCode.NotFound);
                }
                if (FailHeader)
                {
                    return Json("{\"error\":\"header failed\"}", HttpStatusCode.BadRequest);
                }
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<style>header</style>") };
            }
            if (path == "/v1/settings")
            {
                CaptureStarted.TrySetResult();
                if (BlockCapture)
                {
                    await ReleaseCapture.Task;
                }
                return FailCapture
                    ? Json("{\"error\":\"capture failed\"}", HttpStatusCode.BadRequest)
                    : Json("{}");
            }
            return Json("{}");
        }

        private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

        private static HttpResponseMessage Json(string content, HttpStatusCode status = HttpStatusCode.OK) => new(status)
        {
            Content = new StringContent(content),
        };
    }
}