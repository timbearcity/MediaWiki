using System.Net;
using System.Net.Mime;
using System.Text;
using Xunit;

namespace TimBearCity.MediaWiki.Tests;

public sealed class MediaWikiClientTransformTests
{
    private const string LintJson =
        """
        [
          {
            "type": "missing-end-tag",
            "dsr": [ 0, 6, 0, 0 ],
            "templateInfo": null,
            "params": { }
          }
        ]
        """;

    [Fact]
    public async Task TransformHtmlToWikitextAsync_NullHtml_ThrowsArgumentNullException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningContent("== Hello world ==", MediaTypeNames.Text.Plain);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.TransformHtmlToWikitextAsync(null!, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("html", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TransformHtmlToWikitextAsync_ReturnsWikitext()
    {
        const string wikitext = "== Hello world ==";

        using var handler = HttpMessageHandlerStub.CreateReturningContent(wikitext, MediaTypeNames.Text.Plain);
        using var httpClient = handler.CreateClient();

        var converted = await new MediaWikiClient(httpClient).TransformHtmlToWikitextAsync(
            "<h2>Hello world</h2>",
            "Wikipedia:Sandbox",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, handler.Request.Method);
        Assert.Equal(
            $"{HttpMessageHandlerStub.DefaultBaseAddress}transform/html/to/wikitext/Wikipedia%3ASandbox",
            handler.Request.RequestUri?.AbsoluteUri);
        Assert.Equal(MediaTypeNames.Text.Plain, Assert.Single(handler.Request.Headers.Accept).MediaType);
        // The serializer escapes the angle brackets, which the wiki reads back as the markup it was handed.
        Assert.Equal("""{"html":"\u003Ch2\u003EHello world\u003C/h2\u003E"}""", handler.RequestBody);
        Assert.Equal(wikitext, converted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TransformWikitextToHtmlAsync_BlankTitle_ThrowsArgumentException(string title)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningContent("<h2>Hello world</h2>", MediaTypeNames.Text.Html);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            client.TransformWikitextToHtmlAsync("== Hello world ==", title, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("title", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TransformWikitextToHtmlAsync_NullWikitext_ThrowsArgumentNullException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningContent("<h2>Hello world</h2>", MediaTypeNames.Text.Html);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.TransformWikitextToHtmlAsync(null!, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("wikitext", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TransformWikitextToHtmlAsync_RejectedByTheWiki_ThrowsMediaWikiException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningStatus(HttpStatusCode.BadRequest);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() =>
            client.TransformWikitextToHtmlAsync("== Hello world ==", cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    }

    [Fact]
    public async Task TransformWikitextToHtmlAsync_ReturnsHtml()
    {
        const string html = "<h2>Hello world</h2>";

        using var handler = HttpMessageHandlerStub.CreateReturningContent(html, MediaTypeNames.Text.Html);
        using var httpClient = handler.CreateClient();

        var rendered = await new MediaWikiClient(httpClient).TransformWikitextToHtmlAsync(
            "== Hello world ==",
            "Wikipedia:Sandbox",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            $"{HttpMessageHandlerStub.DefaultBaseAddress}transform/wikitext/to/html/Wikipedia%3ASandbox",
            handler.Request.RequestUri?.AbsoluteUri);
        Assert.Equal(MediaTypeNames.Application.Json, handler.Request.Content?.Headers.ContentType?.ToString());
        // The wiki negotiates this endpoint on Accept, and refuses the JSON default with 406.
        Assert.Equal(MediaTypeNames.Text.Html, Assert.Single(handler.Request.Headers.Accept).MediaType);
        Assert.Equal(html, rendered);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TransformWikitextToHtmlAsync_RevisionIdNotPositive_ThrowsArgumentOutOfRangeException(long revisionId)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningContent("<h2>Hello world</h2>", MediaTypeNames.Text.Html);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.TransformWikitextToHtmlAsync(
            "== Hello world ==",
            "Wikipedia:Sandbox",
            revisionId,
            TestContext.Current.CancellationToken));

        Assert.Equal("revisionId", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TransformWikitextToHtmlAsync_RevisionIdWithoutTitle_ThrowsArgumentException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningContent("<h2>Hello world</h2>", MediaTypeNames.Text.Html);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => client.TransformWikitextToHtmlAsync(
            "== Hello world ==",
            revisionId: 1234567,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("title", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TransformWikitextToHtmlAsync_SendsBodyWithContentLength()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningContent("<h2>Hello world</h2>", MediaTypeNames.Text.Html);
        using var httpClient = handler.CreateClient();

        await new MediaWikiClient(httpClient).TransformWikitextToHtmlAsync("== Hello world ==", cancellationToken: TestContext.Current.CancellationToken);

        // MediaWiki rejects a chunked body as if none had been sent, so the length has to be known up front.
        Assert.NotNull(handler.Request.Content);
        Assert.NotNull(handler.RequestBody);
        Assert.Equal(Encoding.UTF8.GetByteCount(handler.RequestBody), handler.Request.Content.Headers.ContentLength);
    }

    [Theory]
    [InlineData(null, null, "")]
    [InlineData("Wikipedia:Sandbox", null, "/Wikipedia%3ASandbox")]
    [InlineData("Wikipedia:Sandbox", 1234567L, "/Wikipedia%3ASandbox/1234567")]
    public async Task TransformWikitextToHtmlAsync_TitleAndRevisionId_PostsToMatchingEndpoint(string? title, long? revisionId, string expectedPathSuffix)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningContent("<h2>Hello world</h2>", MediaTypeNames.Text.Html);
        using var httpClient = handler.CreateClient();

        await new MediaWikiClient(httpClient).TransformWikitextToHtmlAsync("== Hello world ==", title, revisionId, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, handler.Request.Method);
        Assert.Equal(
            $"{HttpMessageHandlerStub.DefaultBaseAddress}transform/wikitext/to/html{expectedPathSuffix}",
            handler.Request.RequestUri?.AbsoluteUri);
        Assert.Equal("""{"wikitext":"== Hello world =="}""", handler.RequestBody);
    }

    [Fact]
    public async Task TransformWikitextToLintAsync_NullWikitext_ThrowsArgumentNullException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(LintJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.TransformWikitextToLintAsync(null!, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("wikitext", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TransformWikitextToLintAsync_RejectedByTheWiki_ThrowsMediaWikiException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningStatus(HttpStatusCode.BadRequest);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<MediaWikiException>(() =>
            client.TransformWikitextToLintAsync("</div>", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TransformWikitextToLintAsync_ReturnsLintErrors()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(LintJson);
        using var httpClient = handler.CreateClient();

        var errors = await new MediaWikiClient(httpClient).TransformWikitextToLintAsync(
            "</div>",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, handler.Request.Method);
        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}transform/wikitext/to/lint", handler.Request.RequestUri?.AbsoluteUri);

        // MediaWiki 1.43 answers a request for application/json with 406, so the wildcard replaces the JSON default.
        Assert.Equal("*/*", Assert.Single(handler.Request.Headers.Accept).MediaType);
        Assert.Equal("""{"wikitext":"\u003C/div\u003E"}""", handler.RequestBody);

        Assert.Equal("missing-end-tag", Assert.Single(errors).Type);
    }
}
