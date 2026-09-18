using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using TimBearCity.MediaWiki.Pages;
using Xunit;

namespace TimBearCity.MediaWiki.Tests;

/// <summary>
/// Covers the <see cref="Activity"/> the client starts per operation under <see cref="MediaWikiClient.ActivitySourceName"/>.
/// </summary>
/// <remarks>
/// An <see cref="ActivityListener"/> is process-wide, so one registered here also sees the activities other test
/// classes start in parallel. Each test therefore runs its call under a parent activity of its own and asserts only
/// over the children of that parent.
/// </remarks>
public sealed class MediaWikiClientDiagnosticsTests
{
    private const string ErrorJson =
        $$"""
          {
            "errorKey": "{{MediaWikiErrorKeys.BadRequest}}",
            "messageTranslations": { "en": "The request was malformed." },
            "httpReason": "Bad Request"
          }
          """;

    /// <summary>
    /// Every operation on <see cref="IMediaWikiClient"/>, keyed by method name, so a method that stops naming its
    /// activity after itself is caught. Keyed rather than listed as delegates so that each row is serializable, and
    /// therefore enumerable on its own in a test explorer.
    /// </summary>
    private static readonly Dictionary<string, Func<IMediaWikiClient, CancellationToken, Task>> OperationsByName = new(StringComparer.Ordinal)
    {
        [nameof(IMediaWikiClient.CompareRevisionsAsync)] = (client, cancellationToken) => client.CompareRevisionsAsync(1, 2, cancellationToken),
        [nameof(IMediaWikiClient.CreatePageAsync)] =
            (client, cancellationToken) => client.CreatePageAsync("Title", "Source", "Comment", cancellationToken: cancellationToken),
        [nameof(IMediaWikiClient.GetFileAsync)] = (client, cancellationToken) => client.GetFileAsync("File.png", cancellationToken),
        [nameof(IMediaWikiClient.GetFileThumbnailsAsync)] = (client, cancellationToken) => client.GetFileThumbnailsAsync("File.png", cancellationToken),
        [nameof(IMediaWikiClient.GetPageAsync)] = (client, cancellationToken) => client.GetPageAsync("Title", cancellationToken),
        [nameof(IMediaWikiClient.GetPageBareAsync)] = (client, cancellationToken) => client.GetPageBareAsync("Title", cancellationToken),
        [nameof(IMediaWikiClient.GetPageFilesAsync)] = (client, cancellationToken) => client.GetPageFilesAsync("Title", cancellationToken),
        [nameof(IMediaWikiClient.GetPageHistoryAsync)] =
            (client, cancellationToken) => client.GetPageHistoryAsync("Title", cancellationToken: cancellationToken),
        [nameof(IMediaWikiClient.GetPageHistoryCountAsync)] = (client, cancellationToken) =>
            client.GetPageHistoryCountAsync("Title", MediaWikiPageHistoryCountType.Edits, cancellationToken: cancellationToken),
        [nameof(IMediaWikiClient.GetPageHtmlAsync)] = (client, cancellationToken) => client.GetPageHtmlAsync("Title", cancellationToken),
        [nameof(IMediaWikiClient.GetPageLanguageLinksAsync)] = (client, cancellationToken) => client.GetPageLanguageLinksAsync("Title", cancellationToken),
        [nameof(IMediaWikiClient.GetPageLintAsync)] = (client, cancellationToken) => client.GetPageLintAsync("Title", cancellationToken),
        [nameof(IMediaWikiClient.GetPageWithHtmlAsync)] = (client, cancellationToken) => client.GetPageWithHtmlAsync("Title", cancellationToken),
        [nameof(IMediaWikiClient.GetRevisionAsync)] = (client, cancellationToken) => client.GetRevisionAsync(1, cancellationToken),
        [nameof(IMediaWikiClient.GetRevisionBareAsync)] = (client, cancellationToken) => client.GetRevisionBareAsync(1, cancellationToken),
        [nameof(IMediaWikiClient.GetRevisionHtmlAsync)] = (client, cancellationToken) => client.GetRevisionHtmlAsync(1, cancellationToken),
        [nameof(IMediaWikiClient.GetRevisionLintAsync)] = (client, cancellationToken) => client.GetRevisionLintAsync(1, cancellationToken),
        [nameof(IMediaWikiClient.GetRevisionWithHtmlAsync)] = (client, cancellationToken) => client.GetRevisionWithHtmlAsync(1, cancellationToken),
        [nameof(IMediaWikiClient.SearchPagesAsync)] = (client, cancellationToken) => client.SearchPagesAsync("query", cancellationToken: cancellationToken),
        [nameof(IMediaWikiClient.SearchTitlesAsync)] = (client, cancellationToken) => client.SearchTitlesAsync("query", cancellationToken: cancellationToken),
        [nameof(IMediaWikiClient.TransformHtmlToWikitextAsync)] = (client, cancellationToken) =>
            client.TransformHtmlToWikitextAsync("<i>html</i>", cancellationToken: cancellationToken),
        [nameof(IMediaWikiClient.TransformWikitextToHtmlAsync)] = (client, cancellationToken) =>
            client.TransformWikitextToHtmlAsync("''wikitext''", cancellationToken: cancellationToken),
        [nameof(IMediaWikiClient.TransformWikitextToLintAsync)] = (client, cancellationToken) =>
            client.TransformWikitextToLintAsync("''wikitext''", cancellationToken: cancellationToken),
        [nameof(IMediaWikiClient.UpdatePageAsync)] =
            (client, cancellationToken) => client.UpdatePageAsync("Title", "Source", "Comment", cancellationToken: cancellationToken)
    };

    public static TheoryData<string> Operations => [.. OperationsByName.Keys];

    [Fact]
    public async Task GetPageAsync_CallerCancels_LeavesActivityUnset()
    {
        using var handler = HttpMessageHandlerStub.CreateBlocking();
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);
        using var recorder = new ActivityRecorder();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetPageAsync("Title", cancellation.Token));

        var activity = Assert.Single(recorder.Activities);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Null(activity.GetTagItem("error.type"));
    }

    [Fact]
    public async Task GetPageAsync_ErrorResponse_MarksActivityFailed()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(ErrorJson, HttpStatusCode.BadRequest);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);
        using var recorder = new ActivityRecorder();

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Title", TestContext.Current.CancellationToken));

        var activity = Assert.Single(recorder.Activities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(exception.Message, activity.StatusDescription);
        Assert.Equal(typeof(MediaWikiException).FullName, activity.GetTagItem("error.type"));
        Assert.Equal(400, activity.GetTagItem("http.response.status_code"));
        Assert.Equal(MediaWikiErrorKeys.BadRequest, activity.GetTagItem("mediawiki.error_key"));
    }

    [Fact]
    public async Task GetPageAsync_HandlerThrowsOtherException_MarksActivityFailed()
    {
        var rejection = new InvalidOperationException("The operation didn't complete within the allowed timeout.");
        using var handler = HttpMessageHandlerStub.CreateThrowing(rejection);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);
        using var recorder = new ActivityRecorder();

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetPageAsync("Title", TestContext.Current.CancellationToken));

        var activity = Assert.Single(recorder.Activities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(rejection.Message, activity.StatusDescription);
        Assert.Equal(typeof(InvalidOperationException).FullName, activity.GetTagItem("error.type"));
        Assert.Null(activity.GetTagItem("http.response.status_code"));
    }

    [Fact]
    public async Task GetPageAsync_MissingPage_TagsErrorKeyWithoutFailing()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentTitle);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);
        using var recorder = new ActivityRecorder();

        var page = await client.GetPageAsync("Missing", TestContext.Current.CancellationToken);

        Assert.Null(page);
        var activity = Assert.Single(recorder.Activities);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Equal(MediaWikiErrorKeys.NonexistentTitle, activity.GetTagItem("mediawiki.error_key"));
        Assert.Null(activity.GetTagItem("error.type"));
    }

    [Fact]
    public async Task GetPageAsync_NoSubscriber_LeavesCallersActivityUntouched()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(ErrorJson, HttpStatusCode.BadRequest);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);
        using var parent = new Activity("caller");
        parent.Start();

        await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Title", TestContext.Current.CancellationToken));

        Assert.Same(parent, Activity.Current);
        Assert.Equal(ActivityStatusCode.Unset, parent.Status);
        Assert.Empty(parent.TagObjects);
    }

    [Fact]
    public async Task GetPageAsync_Subscribed_ParentsTheHttpRequest()
    {
        string? activityIdAtRequest = null;
        using var handler = HttpMessageHandlerStub.CreateResponding(_ =>
        {
            activityIdAtRequest = Activity.Current?.Id;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(EinsteinPage.Json) };
        });
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);
        using var recorder = new ActivityRecorder();

        await client.GetPageAsync("Title", TestContext.Current.CancellationToken);

        // The handler pipeline runs while the operation's activity is current, so the HttpClient span nests under it.
        var activity = Assert.Single(recorder.Activities);
        Assert.Equal(activity.Id, activityIdAtRequest);
    }

    [Fact]
    public async Task GetPageAsync_Subscribed_TagsWikiHostAndSucceeds()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.Json);
        using var httpClient = handler.CreateClient("https://commons.example/w/rest.php/v1/");
        var client = new MediaWikiClient(httpClient);
        using var recorder = new ActivityRecorder();

        await client.GetPageAsync("Title", TestContext.Current.CancellationToken);

        var activity = Assert.Single(recorder.Activities);
        Assert.Equal("commons.example", activity.GetTagItem("server.address"));
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Null(activity.GetTagItem("error.type"));
        Assert.Null(activity.GetTagItem("mediawiki.error_key"));
    }

    [Fact]
    public async Task GetPageAsync_SubscriberDeclinesToSample_RunsWithoutActivity()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.Json);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);
        using var parent = new Activity("caller");
        parent.Start();
        using var listener = new ActivityListener();
        listener.ShouldListenTo = source => source.Name == MediaWikiClient.ActivitySourceName;
        listener.Sample = (ref _) => ActivitySamplingResult.None;
        ActivitySource.AddActivityListener(listener);

        var page = await client.GetPageAsync("Title", TestContext.Current.CancellationToken);

        Assert.NotNull(page);
        Assert.Same(parent, Activity.Current);
    }

    [Fact]
    public async Task GetPageAsync_TransportFailure_MarksActivityFailedWithoutStatusCode()
    {
        using var handler = HttpMessageHandlerStub.CreateThrowing(new HttpRequestException("Connection refused"));
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);
        using var recorder = new ActivityRecorder();

        await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Title", TestContext.Current.CancellationToken));

        var activity = Assert.Single(recorder.Activities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(typeof(MediaWikiException).FullName, activity.GetTagItem("error.type"));
        Assert.Null(activity.GetTagItem("http.response.status_code"));
        Assert.Null(activity.GetTagItem("mediawiki.error_key"));
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Operation_Subscribed_StartsActivityNamedAfterOperation(string methodName)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningStatus(HttpStatusCode.InternalServerError);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);
        using var recorder = new ActivityRecorder();

        await Assert.ThrowsAsync<MediaWikiException>(() => OperationsByName[methodName](client, TestContext.Current.CancellationToken));

        var activity = Assert.Single(recorder.Activities);
        Assert.Equal(methodName[..^"Async".Length], activity.OperationName);
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal(MediaWikiClient.ActivitySourceName, activity.Source.Name);
    }

    /// <summary>
    /// Subscribes to the client's source for the lifetime of one test, under a parent activity that keeps the
    /// activities of other tests out of <see cref="Activities"/>.
    /// </summary>
    private sealed class ActivityRecorder : IDisposable
    {
        private readonly ActivityListener _listener;
        private readonly Activity _parent;
        private readonly ConcurrentQueue<Activity> _stopped = [];

        public ActivityRecorder()
        {
            _parent = new Activity("test");
            _parent.Start();

            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == MediaWikiClient.ActivitySourceName,
                Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = _stopped.Enqueue
            };

            ActivitySource.AddActivityListener(_listener);
        }

        /// <summary>The activities the client started under this test's parent, oldest first.</summary>
        public IReadOnlyList<Activity> Activities => [.. _stopped.Where(activity => activity.ParentId == _parent.Id)];

        public void Dispose()
        {
            _listener.Dispose();
            _parent.Dispose();
        }
    }
}
