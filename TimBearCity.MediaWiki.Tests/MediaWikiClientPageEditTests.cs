using System.Net;
using System.Net.Mime;
using System.Text.Json;
using Xunit;

namespace TimBearCity.MediaWiki.Tests;

public sealed class MediaWikiClientPageEditTests
{
    /// <summary>A CSRF token shaped as MediaWiki issues them: forty hex digits and the "+\" suffix only cookie-based callers need.</summary>
    private const string CsrfToken = @"9ed1499d99c0c34c73faa07157b3b6075b427365+\";

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreatePageAsync_BlankTitle_ThrowsArgumentException(string title)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.Json, HttpStatusCode.Created);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.CreatePageAsync(title, "Hello, world.", "Testing the REST API", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreatePageAsync_NullComment_ThrowsArgumentNullException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.Json, HttpStatusCode.Created);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.CreatePageAsync("Wikipedia:Sandbox", "Hello, world.", null!, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("comment", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreatePageAsync_NullSource_ThrowsArgumentNullException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.Json, HttpStatusCode.Created);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.CreatePageAsync("Wikipedia:Sandbox", null!, "Testing the REST API", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("source", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreatePageAsync_OptionalArgumentsOmitted_SendsOnlyTheRequiredFields()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.Json, HttpStatusCode.Created);
        using var httpClient = handler.CreateClient();

        await new MediaWikiClient(httpClient).CreatePageAsync(
            "Wikipedia:Sandbox",
            string.Empty,
            string.Empty,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("""{"title":"Wikipedia:Sandbox","source":"","comment":""}""", handler.RequestBody);
    }

    [Fact]
    public async Task CreatePageAsync_PageAlreadyExists_ThrowsMediaWikiException()
    {
        const string conflictJson =
            $$"""
              {
                "errorKey": "{{MediaWikiErrorKeys.ArticleExists}}",
                "messageTranslations": { "en": "The article you tried to create has been created already." },
                "httpReason": "Conflict"
              }
              """;

        using var handler = HttpMessageHandlerStub.CreateReturningJson(conflictJson, HttpStatusCode.Conflict);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() =>
            client.CreatePageAsync("Albert Einstein", "Hello, world.", "Testing the REST API", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal(MediaWikiErrorKeys.ArticleExists, exception.ErrorKey);
        Assert.False(exception.IsTransient);
    }

    [Fact]
    public async Task CreatePageAsync_ReturnsCreatedPage()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.Json, HttpStatusCode.Created);
        using var httpClient = handler.CreateClient();

        var page = await new MediaWikiClient(httpClient).CreatePageAsync(
            "Wikipedia:Sandbox",
            "Hello, world.",
            "Testing the REST API",
            "wikitext",
            CsrfToken,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, handler.Request.Method);
        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}page", handler.Request.RequestUri?.AbsoluteUri);
        Assert.Equal(MediaTypeNames.Application.Json, handler.Request.Content?.Headers.ContentType?.ToString());
        Assert.Equal(
            $$"""{"title":"Wikipedia:Sandbox","source":"Hello, world.","comment":"Testing the REST API","content_model":"wikitext","token":"{{JsonEncodedText.Encode(CsrfToken)}}"}""",
            handler.RequestBody);

        Assert.NotNull(page);
        Assert.Equal(EinsteinPage.Id, page.Id);
        Assert.Equal(EinsteinPage.LatestRevisionId, page.Latest.Id);
        Assert.Equal(EinsteinPage.Source, page.Source);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdatePageAsync_BlankKey_ThrowsArgumentException(string key)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.Json);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.UpdatePageAsync(key, "Hello, world.", "Testing the REST API", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UpdatePageAsync_EditConflict_ThrowsMediaWikiException()
    {
        const string conflictJson =
            $$"""
              {
                "errorKey": "{{MediaWikiErrorKeys.EditConflict}}",
                "messageTranslations": { "en": "Edit conflict." },
                "httpReason": "Conflict"
              }
              """;

        using var handler = HttpMessageHandlerStub.CreateReturningJson(conflictJson, HttpStatusCode.Conflict);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() =>
            client.UpdatePageAsync("Albert_Einstein", "Hello, world.", "Testing the REST API", 1234567,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal(MediaWikiErrorKeys.EditConflict, exception.ErrorKey);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdatePageAsync_LatestRevisionIdNotPositive_ThrowsArgumentOutOfRangeException(long latestRevisionId)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.Json);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.UpdatePageAsync("Albert_Einstein", "Hello, world.", "Testing the REST API", latestRevisionId,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("latestRevisionId", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UpdatePageAsync_NoLatestRevisionId_OmitsBaseRevisionFromBody()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.Json, HttpStatusCode.Created);
        using var httpClient = handler.CreateClient();

        // Without a base revision the endpoint creates the page, so the wiki answers 201 rather than 200.
        var page = await new MediaWikiClient(httpClient).UpdatePageAsync(
            "Wikipedia:Sandbox",
            "Hello, world.",
            "Testing the REST API",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("""{"source":"Hello, world.","comment":"Testing the REST API"}""", handler.RequestBody);
        Assert.Equal(EinsteinPage.Id, page.Id);
    }

    [Fact]
    public async Task UpdatePageAsync_NullComment_ThrowsArgumentNullException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.Json);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.UpdatePageAsync("Albert_Einstein", "Hello, world.", null!, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("comment", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UpdatePageAsync_NullSource_ThrowsArgumentNullException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.Json);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.UpdatePageAsync("Albert_Einstein", null!, "Testing the REST API", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("source", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task UpdatePageAsync_PageDeletedSinceItWasRead_ThrowsMediaWikiException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningStatus(HttpStatusCode.NotFound);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        // Unlike the read endpoints, a page that is not there is a failed edit rather than an absent result.
        var exception = await Assert.ThrowsAsync<MediaWikiException>(() =>
            client.UpdatePageAsync("Nonexistent", "Hello, world.", "Testing the REST API", 1234567, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task UpdatePageAsync_ReturnsUpdatedPage()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.Json);
        using var httpClient = handler.CreateClient();

        var page = await new MediaWikiClient(httpClient).UpdatePageAsync(
            "Albert Einstein",
            "Hello, world.",
            "Testing the REST API",
            1234566,
            "wikitext",
            CsrfToken,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Put, handler.Request.Method);
        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}page/Albert_Einstein", handler.Request.RequestUri?.AbsoluteUri);
        Assert.Equal(MediaTypeNames.Application.Json, handler.Request.Content?.Headers.ContentType?.ToString());
        Assert.Equal(
            $$"""{"source":"Hello, world.","comment":"Testing the REST API","latest":{"id":1234566},"content_model":"wikitext","token":"{{JsonEncodedText.Encode(CsrfToken)}}"}""",
            handler.RequestBody);

        Assert.Equal(EinsteinPage.Id, page.Id);
        Assert.Equal(EinsteinPage.LatestRevisionId, page.Latest.Id);
    }
}
