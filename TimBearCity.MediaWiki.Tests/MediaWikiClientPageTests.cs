using System.Net.Mime;
using Xunit;

namespace TimBearCity.MediaWiki.Tests;

public sealed class MediaWikiClientPageTests
{
    /// <summary>
    /// A page in the <c>MediaWiki:</c> namespace as Wikimedia wikis answer it: the page exists and has content, but the
    /// page and revision ids are 0 and the revision timestamp is null.
    /// </summary>
    private const string MediaWikiNamespacePageJson =
        """
        {
          "id": 0,
          "key": "MediaWiki:Common.css",
          "title": "MediaWiki:Common.css",
          "latest": { "id": 0, "timestamp": null },
          "content_model": "css",
          "license": { "title": "CC BY-SA 4.0", "url": "https://creativecommons.org/licenses/by-sa/4.0/" },
          "source": "/* CSS placed here will be applied to all skins */"
        }
        """;

    private const string RedirectPageJson =
        """
        {
          "id": 9229,
          "key": "Einstein",
          "title": "Einstein",
          "latest": { "id": 1234568, "timestamp": "2024-01-02T03:04:05Z" },
          "content_model": "wikitext",
          "license": { "title": "CC BY-SA 4.0", "url": "https://creativecommons.org/licenses/by-sa/4.0/" },
          "source": "#REDIRECT [[Albert Einstein]]",
          "redirect_target": "/w/rest.php/v1/page/Albert_Einstein?redirect=no"
        }
        """;

    private const string UnlicensedPageJson =
        """
        {
          "id": 1,
          "key": "Main_Page",
          "title": "Main Page",
          "latest": { "id": 2, "timestamp": "2024-01-02T03:04:05Z" },
          "content_model": "wikitext",
          "license": { "title": null, "url": null },
          "source": "Welcome."
        }
        """;

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPageAsync_BlankKey_ThrowsArgumentException(string key)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.Json);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetPageAsync(key, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("Albert Einstein", "Albert%20Einstein")]
    // Each character outside ASCII goes out as its percent-encoded UTF-8 bytes.
    [InlineData("太陽系", "%E5%A4%AA%E9%99%BD%E7%B3%BB")]
    public async Task GetPageAsync_KeyNeedingEscaping_RequestsEscapedPageEndpoint(string key, string expectedSegment)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.Json);
        using var httpClient = handler.CreateClient();

        await new MediaWikiClient(httpClient).GetPageAsync(key, TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Get, handler.Request.Method);
        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}page/{expectedSegment}", handler.Request.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task GetPageAsync_MalformedKey_ReturnsNull()
    {
        // The page endpoints answer a title no page could have with a key of its own, which reads as absence all the same.
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.InvalidTitle);
        using var httpClient = handler.CreateClient();

        var page = await new MediaWikiClient(httpClient).GetPageAsync("<>", TestContext.Current.CancellationToken);

        Assert.Null(page);
    }

    [Fact]
    public async Task GetPageAsync_MediaWikiNamespacePage_ReturnsPageWithPlaceholderRevision()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(MediaWikiNamespacePageJson);
        using var httpClient = handler.CreateClient();

        var page = await new MediaWikiClient(httpClient).GetPageAsync("MediaWiki:Common.css", TestContext.Current.CancellationToken);

        Assert.NotNull(page);
        Assert.Equal(0, page.Id);
        Assert.Equal(0, page.Latest.Id);
        Assert.Null(page.Latest.Timestamp);
        Assert.Equal("css", page.ContentModel);
        Assert.Equal("/* CSS placed here will be applied to all skins */", page.Source);
    }

    [Fact]
    public async Task GetPageAsync_PageDoesNotExist_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentTitle);
        using var httpClient = handler.CreateClient();

        var page = await new MediaWikiClient(httpClient).GetPageAsync("Nonexistent", TestContext.Current.CancellationToken);

        Assert.Null(page);
    }

    [Fact]
    public async Task GetPageAsync_RedirectPage_ReturnsRedirectTarget()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(RedirectPageJson);
        using var httpClient = handler.CreateClient();

        var page = await new MediaWikiClient(httpClient).GetPageAsync("Einstein", TestContext.Current.CancellationToken);

        Assert.NotNull(page);
        Assert.Equal("/w/rest.php/v1/page/Albert_Einstein?redirect=no", page.RedirectTarget);
        Assert.Equal("#REDIRECT [[Albert Einstein]]", page.Source);
    }

    [Fact]
    public async Task GetPageAsync_ReturnsPage()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.Json);
        using var httpClient = handler.CreateClient();

        var page = await new MediaWikiClient(httpClient).GetPageAsync(EinsteinPage.Key, TestContext.Current.CancellationToken);

        Assert.NotNull(page);
        Assert.Equal(EinsteinPage.Id, page.Id);
        Assert.Equal(EinsteinPage.Title, page.Title);
        Assert.Equal(EinsteinPage.ContentModel, page.ContentModel);
        Assert.Equal(EinsteinPage.Source, page.Source);
        Assert.Equal(EinsteinPage.LatestRevisionId, page.Latest.Id);
        Assert.Equal(EinsteinPage.LicenseTitle, page.License.Title);
        Assert.Null(page.RedirectTarget);
    }

    [Fact]
    public async Task GetPageAsync_WikiWithoutConfiguredLicense_ReturnsPageWithNullLicenseFields()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(UnlicensedPageJson);
        using var httpClient = handler.CreateClient();

        var page = await new MediaWikiClient(httpClient).GetPageAsync("Main_Page", TestContext.Current.CancellationToken);

        Assert.NotNull(page);
        Assert.Equal("Welcome.", page.Source);
        Assert.Null(page.License.Title);
        Assert.Null(page.License.Url);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPageBareAsync_BlankKey_ThrowsArgumentException(string key)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.BareJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetPageBareAsync(key, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetPageBareAsync_PageDoesNotExist_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentTitle);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).GetPageBareAsync("Nonexistent", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetPageBareAsync_ReturnsMetadataAndHtmlUrl()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.BareJson);
        using var httpClient = handler.CreateClient();

        var page = await new MediaWikiClient(httpClient).GetPageBareAsync("Albert Einstein", TestContext.Current.CancellationToken);

        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}page/Albert%20Einstein/bare", handler.Request.RequestUri?.AbsoluteUri);
        Assert.NotNull(page);
        Assert.Equal(EinsteinPage.Id, page.Id);
        Assert.Equal(EinsteinPage.HtmlUrl, page.HtmlUrl);
        Assert.Null(page.RedirectTarget);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPageHtmlAsync_BlankKey_ThrowsArgumentException(string key)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningContent("<p>x</p>", MediaTypeNames.Text.Html);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetPageHtmlAsync(key, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetPageHtmlAsync_PageDoesNotExist_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentTitle);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).GetPageHtmlAsync("Nonexistent", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetPageHtmlAsync_ReturnsHtml()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningContent(EinsteinPage.Html, MediaTypeNames.Text.Html);
        using var httpClient = handler.CreateClient();

        var htmlResponse = await new MediaWikiClient(httpClient).GetPageHtmlAsync("Albert Einstein", TestContext.Current.CancellationToken);

        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}page/Albert%20Einstein/html", handler.Request.RequestUri?.AbsoluteUri);
        Assert.Equal(MediaTypeNames.Text.Html, Assert.Single(handler.Request.Headers.Accept).MediaType);
        Assert.Equal(EinsteinPage.Html, htmlResponse);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPageWithHtmlAsync_BlankKey_ThrowsArgumentException(string key)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.WithHtmlJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetPageWithHtmlAsync(key, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetPageWithHtmlAsync_PageDoesNotExist_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentTitle);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).GetPageWithHtmlAsync("Nonexistent", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetPageWithHtmlAsync_ReturnsMetadataAndHtml()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.WithHtmlJson);
        using var httpClient = handler.CreateClient();

        var page = await new MediaWikiClient(httpClient).GetPageWithHtmlAsync("Albert Einstein", TestContext.Current.CancellationToken);

        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}page/Albert%20Einstein/with_html", handler.Request.RequestUri?.AbsoluteUri);
        Assert.NotNull(page);
        Assert.Equal(EinsteinPage.Id, page.Id);
        Assert.Equal(EinsteinPage.Html, page.Html);
    }
}
