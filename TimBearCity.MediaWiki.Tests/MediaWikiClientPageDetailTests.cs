using Xunit;

namespace TimBearCity.MediaWiki.Tests;

public sealed class MediaWikiClientPageDetailTests
{
    private const string LanguageLinksJson =
        """
        [
          { "code": "de", "name": "Deutsch", "key": "Albert_Einstein", "title": "Albert Einstein" },
          { "code": "simple", "name": "Simple English", "key": "Albert_Einstein", "title": "Albert Einstein" }
        ]
        """;

    private const string LintJson =
        """
        [
          {
            "type": "obsolete-tag",
            "dsr": [ 0, 7, 3, 4 ],
            "templateInfo": { "multiPartTemplateBlock": true },
            "params": { }
          }
        ]
        """;

    private const string MediaJson =
        """
        {
          "files": [
            {
              "title": "File:Einstein 1921.jpg",
              "file_description_url": "//commons.wikimedia.org/wiki/File:Einstein_1921.jpg",
              "latest": {
                "timestamp": "2022-08-28T12:18:48Z",
                "user": { "id": 234501, "name": "Archivist" }
              },
              "preferred": {
                "mediatype": "BITMAP",
                "size": null,
                "width": 800,
                "height": 1000,
                "duration": null,
                "url": "//upload.wikimedia.org/einstein-800.jpg"
              },
              "original": {
                "mediatype": "BITMAP",
                "size": 4022330,
                "width": 1920,
                "height": 2400,
                "duration": null,
                "url": "//upload.wikimedia.org/einstein.jpg"
              }
            },
            {
              "title": "File:Einstein voice.ogg",
              "file_description_url": "//commons.wikimedia.org/wiki/File:Einstein_voice.ogg",
              "latest": null,
              "preferred": null,
              "original": {
                "mediatype": "AUDIO",
                "size": 51960,
                "width": null,
                "height": null,
                "duration": 51.96,
                "url": "//upload.wikimedia.org/einstein.ogg"
              }
            }
          ]
        }
        """;

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPageFilesAsync_BlankKey_ThrowsArgumentException(string key)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(MediaJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetPageFilesAsync(key, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetPageFilesAsync_PageDoesNotExist_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentTitle);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).GetPageFilesAsync("Nonexistent", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetPageFilesAsync_ReturnsFiles()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(MediaJson);
        using var httpClient = handler.CreateClient();

        var files = await new MediaWikiClient(httpClient).GetPageFilesAsync("Albert Einstein", TestContext.Current.CancellationToken);

        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}page/Albert%20Einstein/links/media", handler.Request.RequestUri?.AbsoluteUri);

        Assert.NotNull(files);
        Assert.Equal(2, files.Count);

        Assert.Equal("File:Einstein 1921.jpg", files[0].Title);
        Assert.Equal("Archivist", files[0].Latest?.User?.Name);
        Assert.Equal("BITMAP", files[0].Preferred?.MediaType);
        Assert.Equal(800, files[0].Preferred?.Width);
        Assert.Null(files[0].Preferred?.Size);
        Assert.Equal(4022330, files[0].Original?.Size);

        // Only the file endpoint reports a thumbnail, and the wiki has no preferred rendition of an audio file.
        Assert.Null(files[0].Thumbnail);
        Assert.Null(files[1].Latest);
        Assert.Null(files[1].Preferred);
        Assert.Equal(51.96, files[1].Original?.Duration);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPageLanguageLinksAsync_BlankKey_ThrowsArgumentException(string key)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(LanguageLinksJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetPageLanguageLinksAsync(key, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetPageLanguageLinksAsync_PageDoesNotExist_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentTitle);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).GetPageLanguageLinksAsync("Nonexistent", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetPageLanguageLinksAsync_ReturnsLinks()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(LanguageLinksJson);
        using var httpClient = handler.CreateClient();

        var links = await new MediaWikiClient(httpClient).GetPageLanguageLinksAsync("Albert Einstein", TestContext.Current.CancellationToken);

        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}page/Albert%20Einstein/links/language", handler.Request.RequestUri?.AbsoluteUri);

        Assert.NotNull(links);
        Assert.Equal(2, links.Count);
        Assert.Equal("de", links[0].Code);
        Assert.Equal("Deutsch", links[0].Name);
        Assert.Equal("Albert_Einstein", links[0].Key);
        Assert.Equal("Simple English", links[1].Name);
        Assert.Equal("Albert Einstein", links[1].Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPageLintAsync_BlankKey_ThrowsArgumentException(string key)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(LintJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetPageLintAsync(key, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetPageLintAsync_PageDoesNotExist_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentTitle);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).GetPageLintAsync("Nonexistent", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetPageLintAsync_ReturnsLintErrors()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(LintJson);
        using var httpClient = handler.CreateClient();

        var errors = await new MediaWikiClient(httpClient).GetPageLintAsync("Solar_System", TestContext.Current.CancellationToken);

        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}page/Solar_System/lint", handler.Request.RequestUri?.AbsoluteUri);

        Assert.NotNull(errors);
        var error = Assert.Single(errors);
        Assert.Equal("obsolete-tag", error.Type);

        // An error spanning several templates leaves none to name.
        Assert.Null(error.TemplateInfo?.Name);
        Assert.True(error.TemplateInfo?.IsMultiPartTemplateBlock);
    }
}
