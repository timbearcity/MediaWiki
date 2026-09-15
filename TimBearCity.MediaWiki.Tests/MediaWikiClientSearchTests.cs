using Xunit;

namespace TimBearCity.MediaWiki.Tests;

public sealed class MediaWikiClientSearchTests
{
    private const string SearchJson =
        """
        {
          "pages": [
            {
              "id": 9228,
              "key": "Albert_Einstein",
              "title": "Albert Einstein",
              "excerpt": "a physicist",
              "description": "German-born physicist",
              "matched_title": "Einstein",
              "anchor": "Early life",
              "thumbnail": {
                "mimetype": "image/jpeg",
                "url": "//upload.wikimedia.org/einstein.jpg",
                "size": 4096,
                "width": 60,
                "height": 82,
                "duration": null
              }
            }
          ]
        }
        """;

    private const string TitleSearchJson =
        """
        {
          "pages": [
            {
              "id": 9228,
              "key": "Albert_Einstein",
              "title": "Albert Einstein",
              "excerpt": "Albert Einstein",
              "description": "German-born physicist",
              "matched_title": null,
              "anchor": null,
              "thumbnail": null
            }
          ]
        }
        """;

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchPagesAsync_BlankQuery_ThrowsArgumentException(string query)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(SearchJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.SearchPagesAsync(query, 10, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(MediaWikiClient.MaxSearchLimit + 1)]
    public async Task SearchPagesAsync_LimitOutOfRange_ThrowsArgumentOutOfRangeException(int limit)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(SearchJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.SearchPagesAsync("physicist", limit, TestContext.Current.CancellationToken));

        Assert.Equal("limit", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SearchPagesAsync_NothingMatched_ReturnsEmpty()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson("""{ "pages": [] }""");
        using var httpClient = handler.CreateClient();

        var pages = await new MediaWikiClient(httpClient).SearchPagesAsync("qwertyuiop", 10, TestContext.Current.CancellationToken);

        Assert.Empty(pages);
    }

    [Fact]
    public async Task SearchPagesAsync_QueryNeedingEscaping_RequestsEscapedSearchEndpoint()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(SearchJson);
        using var httpClient = handler.CreateClient();

        await new MediaWikiClient(httpClient).SearchPagesAsync("cats & dogs", 5, TestContext.Current.CancellationToken);

        Assert.Equal(
            $"{HttpMessageHandlerStub.DefaultBaseAddress}search/page?q=cats%20%26%20dogs&limit=5",
            handler.Request.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task SearchPagesAsync_ReturnsMatchingPages()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(SearchJson);
        using var httpClient = handler.CreateClient();

        var pages = await new MediaWikiClient(httpClient).SearchPagesAsync("physicist", 10, TestContext.Current.CancellationToken);

        var page = Assert.Single(pages);
        Assert.Equal(9228, page.Id);
        Assert.Equal("Albert_Einstein", page.Key);
        Assert.Equal("German-born physicist", page.Description);
        Assert.Equal("Einstein", page.MatchedTitle);
        Assert.Equal("Early life", page.Anchor);

        Assert.NotNull(page.Thumbnail);
        Assert.Equal("image/jpeg", page.Thumbnail.MimeType);
        Assert.Equal("//upload.wikimedia.org/einstein.jpg", page.Thumbnail.Url);
        Assert.Equal(4096, page.Thumbnail.Size);
        Assert.Equal(60, page.Thumbnail.Width);
        Assert.Equal(82, page.Thumbnail.Height);
        Assert.Null(page.Thumbnail.Duration);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchTitlesAsync_BlankQuery_ThrowsArgumentException(string query)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(TitleSearchJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.SearchTitlesAsync(query, 10, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(MediaWikiClient.MaxSearchLimit + 1)]
    public async Task SearchTitlesAsync_LimitOutOfRange_ThrowsArgumentOutOfRangeException(int limit)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(TitleSearchJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.SearchTitlesAsync("Einst", limit, TestContext.Current.CancellationToken));

        Assert.Equal("limit", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SearchTitlesAsync_ReturnsMatchingTitles()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(TitleSearchJson);
        using var httpClient = handler.CreateClient();

        var pages = await new MediaWikiClient(httpClient).SearchTitlesAsync("Einst", 5, TestContext.Current.CancellationToken);

        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}search/title?q=Einst&limit=5", handler.Request.RequestUri?.AbsoluteUri);

        // A title search puts the title where a full-text search puts an excerpt of the matching content.
        var page = Assert.Single(pages);
        Assert.Equal("Albert Einstein", page.Excerpt);
        Assert.Null(page.Anchor);
        Assert.Null(page.Thumbnail);
    }
}
