using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.Json;
using TimBearCity.MediaWiki.Pages;
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

    /// <summary>The members of a page body other than <c>latest</c> and <c>source</c>, which a test writes out as its case needs them.</summary>
    private const string PageMembersJson =
        """
        "id": 9228, "key": "Albert_Einstein", "title": "Albert Einstein", "content_model": "wikitext", "license": { "title": null, "url": null }
        """;

    /// <summary>A status code outside the well-known set, so <c>HttpResponseMessage.ReasonPhrase</c> stays null.</summary>
    private const HttpStatusCode UnknownStatusCode = (HttpStatusCode)599;

    [Theory]
    // A member the record requires is not in the body at all.
    [InlineData($$"""{ {{PageMembersJson}}, "latest": { "id": 1, "timestamp": null } }""", "'source'")]
    // The same one level down, in a record the page is made of.
    [InlineData($$"""{ {{PageMembersJson}}, "latest": { "timestamp": null }, "source": "x" }""", "'id'")]
    // The member is there, and null where the record does not allow it.
    [InlineData($$"""{ {{PageMembersJson}}, "latest": { "id": 1, "timestamp": null }, "source": null }""", "'Source'")]
    public async Task GetPageAsync_BodyBreaksTheRecordsContract_ThrowsMediaWikiException(string json, string member)
    {
        // Without the serializer context's two Respect options, each of these binds, and the caller is handed a page with a null or a zero in it.
        using var handler = HttpMessageHandlerStub.CreateReturningJson(json);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, exception.StatusCode);
        Assert.IsType<JsonException>(exception.InnerException);
        Assert.Contains("could not be read as MediaWikiPage", exception.Message, StringComparison.Ordinal);
        Assert.Contains(member, exception.Message, StringComparison.Ordinal);
    }

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
    public async Task GetPageAsync_CanceledWithoutATimeout_DoesNotCallItATimeout()
    {
        // A handler with a deadline of its own, or an HttpClient that is canceled or disposed, ends the request with a
        // cancellation that wraps no TimeoutException.
        using var handler = HttpMessageHandlerStub.CreateThrowing(new TaskCanceledException());
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.DoesNotContain("timed out", exception.Message, StringComparison.Ordinal);
        Assert.Contains("was canceled after 00:00:00", exception.Message, StringComparison.Ordinal);
        Assert.IsType<TaskCanceledException>(exception.InnerException);
    }

    [Fact]
    public async Task GetPageAsync_EndpointUnknownToWiki_NamesBothCauses()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NoMatch);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        // A wiki answers the same way for a base URL without its "/v1/" as for an endpoint it is too old to serve.
        Assert.Contains("MediaWiki version predates it", exception.Message, StringComparison.Ordinal);
        Assert.Contains("base URL", exception.Message, StringComparison.Ordinal);
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
    [InlineData("""{ "messageTranslations": { "de": "Fehler", "en": "Error" } }""", "Error")]
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
    public async Task GetPageAsync_HandlerThrowsOtherException_PropagatesItUnwrapped()
    {
        // A resilience handler that gives up throws its own exception type; it is the caller's to catch, so it is not translated.
        var rejection = new InvalidOperationException("The operation didn't complete within the allowed timeout.");
        using var handler = HttpMessageHandlerStub.CreateThrowing(rejection);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Same(rejection, exception);
    }

    [Fact]
    public async Task GetPageAsync_NoMatchUnderErrorSchema2_ThrowsWithErrorCodeAsErrorKey()
    {
        // Error schema 2.0 names the key errorCode; read under the old name only, it would be missing, and the hint would blame the base URL.
        const string errorJson =
            $$"""
              {
                "errorCode": "{{MediaWikiErrorKeys.NoMatch}}",
                "message": "The requested relative path (/v1/page/Albert_Einstein) did not match any known handler",
                "messageTranslations": { "en": "The requested relative path (/v1/page/Albert_Einstein) did not match any known handler" },
                "httpCode": 404,
                "tracing": { "module": "mediawiki", "method": "get", "uri": "/w/rest.php/v1/page/Albert_Einstein", "request_id": "a6b3d89e" }
              }
              """;
        using var handler = HttpMessageHandlerStub.CreateReturningJson(errorJson, HttpStatusCode.NotFound);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Equal(MediaWikiErrorKeys.NoMatch, exception.ErrorKey);
        Assert.Contains("no such endpoint", exception.Message, StringComparison.Ordinal);
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

    [Fact]
    public async Task GetPageAsync_NotFoundInUnsupportedCharset_ThrowsMediaWikiException()
    {
        // The same misdirected request, but the error page names a code page .NET has no encoding for without a provider.
        using var handler = HttpMessageHandlerStub.CreateReturningContentInUnsupportedCharset(
            "<html><body><h1>Not Found</h1></body></html>",
            MediaTypeNames.Text.Html,
            HttpStatusCode.NotFound);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Null(exception.ErrorKey);
        Assert.Contains("check the base URL", exception.Message, StringComparison.Ordinal);
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
    // The 404 MediaWiki sends for a route it does not serve, as an older version does for the newer endpoints, and any
    // version does for a base URL without the "/v1/" segment.
    [InlineData(MediaWikiErrorKeys.NoMatch, "no such endpoint")]
    // A path outside the REST API's root, as a web server that hands rest.php some other path produces.
    [InlineData(MediaWikiErrorKeys.PrefixMismatch, "did not recognize the path")]
    // A path no REST module serves, which MediaWiki sends only when not even its prefix-less module is there.
    [InlineData(MediaWikiErrorKeys.UnknownModule, "did not recognize the path")]
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

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task GetPageAsync_StatusWorthRetrying_ReportsTransient(HttpStatusCode statusCode)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningStatus(statusCode);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Equal(statusCode, exception.StatusCode);
        Assert.True(exception.IsTransient);
    }

    [Fact]
    public async Task GetPageAsync_SuccessInUnsupportedCharset_ThrowsMediaWikiException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningContentInUnsupportedCharset("{}", MediaTypeNames.Application.Json);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, exception.StatusCode);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("could not be read as MediaWikiPage", exception.Message, StringComparison.Ordinal);
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

    [Theory]
    [InlineData(30_000)]
    [InlineData(Timeout.Infinite)]
    public async Task GetPageAsync_TimeoutOtherThanTheClients_ReportsTheTimeThatPassed(int clientTimeoutMilliseconds)
    {
        // What the connection pool throws when SocketsHttpHandler.ConnectTimeout expires, here at once and so long before the client's own limit.
        var connectTimeout = new TaskCanceledException(
            "The operation was canceled.",
            new TimeoutException("A connection could not be established within the configured ConnectTimeout."));
        using var handler = HttpMessageHandlerStub.CreateThrowing(connectTimeout);
        using var httpClient = handler.CreateClient(timeout: TimeSpan.FromMilliseconds(clientTimeoutMilliseconds));
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Contains("timed out after 00:00:00", exception.Message, StringComparison.Ordinal);
        Assert.True(exception.IsTransient);
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
    public async Task GetPageHistoryCountAsync_TooManyRevisions_ReportsNotTransient()
    {
        const string errorJson = $$"""
                                   {
                                     "errorKey": "{{MediaWikiErrorKeys.PageHistoryCountTooManyRevisions}}",
                                     "messageTranslations": { "en": "The specified title contains too many revisions to retrieve this count." },
                                     "httpCode": 500,
                                     "httpReason": "Internal Server Error"
                                   }
                                   """;
        using var handler = HttpMessageHandlerStub.CreateReturningJson(errorJson, HttpStatusCode.InternalServerError);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() =>
            client.GetPageHistoryCountAsync("Albert_Einstein", MediaWikiPageHistoryCountType.Minor, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Equal(MediaWikiErrorKeys.PageHistoryCountTooManyRevisions, exception.ErrorKey);
        Assert.False(exception.IsTransient);
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
    public async Task GetPageHtmlAsync_ServerErrorWhoseKeyDoesNotBlameTheRequest_ReportsTransient()
    {
        // What the wiki answers when Parsoid fails: a 500 with an error key, but the wiki's trouble rather than the request's.
        const string errorJson = $$"""
                                   {
                                     "errorKey": "{{MediaWikiErrorKeys.HtmlBackendError}}",
                                     "messageTranslations": { "en": "Unable to fetch Parsoid HTML: backend unavailable" },
                                     "httpCode": 500,
                                     "httpReason": "Internal Server Error"
                                   }
                                   """;
        using var handler = HttpMessageHandlerStub.CreateReturningJson(errorJson, HttpStatusCode.InternalServerError);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageHtmlAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Equal(MediaWikiErrorKeys.HtmlBackendError, exception.ErrorKey);
        Assert.True(exception.IsTransient);
    }

    [Fact]
    public async Task GetPageHtmlAsync_SuccessInUnsupportedCharset_ThrowsMediaWikiException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningContentInUnsupportedCharset("<p>x</p>", MediaTypeNames.Text.Html);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageHtmlAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, exception.StatusCode);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("could not be read as text", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("null", "was empty; expected IReadOnlyList<MediaWikiLintError>.")]
    [InlineData("{}", "could not be read as IReadOnlyList<MediaWikiLintError>: ")]
    public async Task GetPageLintAsync_UnreadableBody_NamesTheListItExpected(string body, string expected)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(body);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageLintAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        // The type's own name is "IReadOnlyList`1", which says neither what was expected nor a list of what.
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchPagesAsync_SearchError_ReportsNotTransient()
    {
        const string errorJson = $$"""
                                   {
                                     "error-keys": ["cirrussearch-regex-syntax-error"],
                                     "errorKey": "{{MediaWikiErrorKeys.SearchError}}",
                                     "messageTranslations": { "en": "Error when returning search results: Regular expression syntax error at unknown: unknown" },
                                     "httpCode": 500,
                                     "httpReason": "Internal Server Error"
                                   }
                                   """;
        using var handler = HttpMessageHandlerStub.CreateReturningJson(errorJson, HttpStatusCode.InternalServerError);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.SearchPagesAsync("insource:/[/", 10, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Equal(MediaWikiErrorKeys.SearchError, exception.ErrorKey);
        Assert.False(exception.IsTransient);
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

    [Fact]
    public async Task TransformWikitextToHtmlAsync_SuccessInUnsupportedCharset_ThrowsMediaWikiException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningContentInUnsupportedCharset("<p>x</p>", MediaTypeNames.Text.Html);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() =>
            client.TransformWikitextToHtmlAsync("x", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, exception.StatusCode);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("could not be read as text", exception.Message, StringComparison.Ordinal);
    }
}
