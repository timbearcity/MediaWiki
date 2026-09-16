using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using Xunit;

namespace TimBearCity.MediaWiki.Tests;

public sealed class MediaWikiClientErrorHandlingTests
{
    private const string ErrorJson =
        $$"""
          {
            "errorKey": "{{MediaWikiErrorKeys.NonexistentTitle}}",
            "messageTranslations": { "en": "The specified title does not exist" },
            "httpReason": "Bad Request"
          }
          """;

    /// <summary>A status code outside the well-known set, so <c>HttpResponseMessage.ReasonPhrase</c> stays null.</summary>
    private const HttpStatusCode UnknownStatusCode = (HttpStatusCode)599;

    [Fact]
    public async Task GetPageAsync_CallerCancels_ThrowsOperationCanceledException()
    {
        using var handler = HttpMessageHandlerStub.CreateBlocking();
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);
        using var cancellation = new CancellationTokenSource();

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetPageAsync("Albert_Einstein", cancellation.Token));
    }

    [Fact]
    public async Task GetPageAsync_ErrorBody_SurfacesErrorDetail()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(ErrorJson, HttpStatusCode.BadRequest);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Bad|Title", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(MediaWikiErrorKeys.NonexistentTitle, exception.ErrorKey);
        Assert.Contains("The specified title does not exist", exception.Message, StringComparison.Ordinal);
        Assert.False(exception.IsTransient);
    }

    [Theory]
    [InlineData("""{ "errorKey": "rest-bad-request" }""", null)]
    [InlineData("""{ "message": "Something broke" }""", "Something broke")]
    [InlineData("""{ "message": "   " }""", null)]
    [InlineData("""{ "messageTranslations": { }, "message": "No translations" }""", "No translations")]
    [InlineData("""{ "messageTranslations": { "de": "Fehler" }, "message": "Ignored" }""", "Fehler")]
    [InlineData("""{ "messageTranslations": { "en": "   ", "de": "Fehler" } }""", "Fehler")]
    [InlineData("""{ "messageTranslations": { "de": "   " }, "message": "Fallback" }""", "Fallback")]
    [InlineData("""{ "messageTranslations": { "de": "   " } }""", null)]
    public async Task GetPageAsync_ErrorBodyShape_SurfacesBestAvailableDetail(string errorJson, string? expectedDetail)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(errorJson, HttpStatusCode.BadRequest);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.EndsWith(expectedDetail is null ? "failed with status 400 Bad Request." : $"failed with status 400 Bad Request. {expectedDetail}",
            exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPageAsync_NoReasonPhraseAndUnreadableErrorBody_ReportsStatusOnly()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningContent("<html>Gateway timeout</html>", MediaTypeNames.Text.Html, UnknownStatusCode);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Null(exception.ErrorKey);
        Assert.EndsWith("failed with status 599.", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("<html><body><h1>Not Found</h1></body></html>")]
    public async Task GetPageAsync_NotFoundWithoutMediaWikiError_ThrowsMediaWikiException(string? body)
    {
        // A web server answering for a base URL that misses the REST API; the status alone must not read as an absent page.
        using var handler = body is null
            ? HttpMessageHandlerStub.CreateReturningStatus(HttpStatusCode.NotFound)
            : HttpMessageHandlerStub.CreateReturningContent(body, MediaTypeNames.Text.Html, HttpStatusCode.NotFound);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Null(exception.ErrorKey);
        Assert.Contains("check the base URL", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    // The 404 MediaWiki sends for a route it does not serve, as an older version does for the newer endpoints.
    [InlineData(MediaWikiErrorKeys.NoMatch, "no such endpoint")]
    // The REST API reached, but at a path it does not own, as a base URL without the "/v1/" segment produces.
    [InlineData(MediaWikiErrorKeys.PrefixMismatch, "did not recognize the path")]
    // Any other key is not a routing problem, so the message names the key and offers no hint.
    [InlineData("rest-something-else", "(rest-something-else).")]
    // A key under which another kind of endpoint reports absence is not absence here: a page endpoint never sends it.
    [InlineData(MediaWikiErrorKeys.CannotLoadFile, "(rest-cannot-load-file).")]
    [InlineData(MediaWikiErrorKeys.CompareNonexistent, "(rest-compare-nonexistent).")]
    [InlineData(MediaWikiErrorKeys.NonexistentTitleRevision, "(rest-nonexistent-title-revision).")]
    public async Task GetPageAsync_NotFoundWithUnrelatedErrorKey_ThrowsMediaWikiException(string errorKey, string expectedDetail)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(errorKey);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(errorKey, exception.ErrorKey);
        Assert.Contains(expectedDetail, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPageAsync_NullJsonBody_ThrowsMediaWikiException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson("null");
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Contains("was empty", exception.Message, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, exception.StatusCode);
    }

    [Fact]
    public async Task GetPageAsync_RateLimited_ReportsTransientWithRetryDelay()
    {
        using var handler = HttpMessageHandlerStub.CreateResponding(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));

            return response;
        });

        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Equal(TimeSpan.FromSeconds(30), exception.RetryAfter);
        Assert.True(exception.IsTransient);
    }

    [Fact]
    public async Task GetPageAsync_ResponseExceedsMaxResponseSize_ThrowsNonTransientMediaWikiException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.Json);
        using var httpClient = handler.CreateClient();
        httpClient.MaxResponseContentBufferSize = 16;
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        // The wiki answered; only the client refused the body, so a retry would fail the same way.
        Assert.Null(exception.StatusCode);
        Assert.False(exception.IsTransient);
        var inner = Assert.IsType<HttpRequestException>(exception.InnerException);
        Assert.Equal(HttpRequestError.ConfigurationLimitExceeded, inner.HttpRequestError);
    }

    [Fact]
    public async Task GetPageAsync_ResponseWithoutReasonPhrase_FallsBackToHttpReasonFromErrorBody()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(ErrorJson, UnknownStatusCode);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Contains("failed with status 599 Bad Request.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPageAsync_RetryAfterDate_ReportsRemainingDelay()
    {
        using var handler = HttpMessageHandlerStub.CreateResponding(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddMinutes(5));

            return response;
        });

        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.NotNull(exception.RetryAfter);
        Assert.InRange(exception.RetryAfter.Value, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task GetPageAsync_RetryAfterDateInThePast_ReportsNoDelay()
    {
        using var handler = HttpMessageHandlerStub.CreateResponding(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddMinutes(-5));

            return response;
        });

        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Equal(TimeSpan.Zero, exception.RetryAfter);
    }

    [Fact]
    public async Task GetPageAsync_Timeout_ThrowsMediaWikiException()
    {
        using var handler = HttpMessageHandlerStub.CreateBlocking();
        using var httpClient = handler.CreateClient(timeout: TimeSpan.FromMilliseconds(50));
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Null(exception.StatusCode);
        Assert.True(exception.IsTransient);
        Assert.Contains("timed out", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPageAsync_TransportFailure_ThrowsTransientMediaWikiException()
    {
        using var handler = HttpMessageHandlerStub.CreateThrowing(new HttpRequestException("No such host is known."));
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Null(exception.StatusCode);
        Assert.True(exception.IsTransient);
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task GetPageAsync_UnreadableBody_ThrowsMediaWikiException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningContent("<html>Gateway timeout</html>", MediaTypeNames.Text.Html);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, exception.StatusCode);
    }

    [Fact]
    public async Task GetPageHtmlAsync_EndpointUnknownToWiki_ThrowsMediaWikiException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NoMatch);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageHtmlAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Equal(MediaWikiErrorKeys.NoMatch, exception.ErrorKey);
    }

    [Fact]
    public async Task GetPageHtmlAsync_JsonErrorBody_SurfacesErrorDetail()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(ErrorJson, HttpStatusCode.BadRequest);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageHtmlAsync("Bad|Title", TestContext.Current.CancellationToken));

        Assert.Equal(MediaWikiErrorKeys.NonexistentTitle, exception.ErrorKey);
    }

    [Fact]
    public async Task SearchPagesAsync_ServerError_ReportsTransient()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningStatus(HttpStatusCode.ServiceUnavailable);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.SearchPagesAsync("physicist", 10, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.True(exception.IsTransient);
    }
}
