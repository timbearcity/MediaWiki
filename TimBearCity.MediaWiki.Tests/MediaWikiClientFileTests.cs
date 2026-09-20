using System.Net;
using Xunit;

namespace TimBearCity.MediaWiki.Tests;

public sealed class MediaWikiClientFileTests
{
    private const string FileJson =
        """
        {
          "title": "Fennec Fox.jpg",
          "file_description_url": "//commons.wikimedia.org/wiki/File:Fennec_Fox.jpg",
          "latest": {
            "timestamp": "2022-08-28T12:18:48Z",
            "user": { "id": 234501, "name": "Archivist" }
          },
          "preferred": {
            "mediatype": "BITMAP",
            "size": null,
            "width": 800,
            "height": 450,
            "duration": null,
            "url": "//upload.wikimedia.org/fennec-800.jpg"
          },
          "original": {
            "mediatype": "BITMAP",
            "size": 4022330,
            "width": 1920,
            "height": 1080,
            "duration": null,
            "url": "//upload.wikimedia.org/fennec.jpg"
          },
          "thumbnail": {
            "mediatype": "BITMAP",
            "size": null,
            "width": 320,
            "height": 180,
            "duration": null,
            "url": "//upload.wikimedia.org/fennec-320.jpg"
          }
        }
        """;

    private const string ThumbnailsJson =
        """
        {
          "title": "Fennec Fox.jpg",
          "original": {
            "mediatype": "BITMAP",
            "width": 1200,
            "height": 800,
            "url": "https://example.org/wiki/Special:FilePath/Fennec_Fox.jpg"
          },
          "thumbnails": [
            {
              "width": 120,
              "height": 80,
              "url": "https://example.org/w/images/thumb/a/ab/Fennec_Fox.jpg/120px-Fennec_Fox.jpg",
              "mime": "image/jpeg",
              "responsive_urls": { "2": "https://example.org/w/images/thumb/a/ab/Fennec_Fox.jpg/240px-Fennec_Fox.jpg" }
            },
            {
              "width": 640,
              "height": 427,
              "url": "https://example.org/w/images/thumb/a/ab/Fennec_Fox.jpg/640px-Fennec_Fox.jpg",
              "mime": "image/jpeg"
            }
          ]
        }
        """;

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetFileAsync_BlankTitle_ThrowsArgumentException(string title)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(FileJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetFileAsync(title, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public async Task GetFileAsync_DotSegmentTitle_ThrowsArgumentException(string title)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(FileJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => client.GetFileAsync(title, TestContext.Current.CancellationToken));

        Assert.Equal("title", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetFileAsync_FileDoesNotExist_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.CannotLoadFile);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).GetFileAsync("File:Nonexistent.jpg", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetFileAsync_FilePageDoesNotExist_ReturnsNull()
    {
        // A file page that was never created is a missing title rather than a file that cannot be loaded.
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentTitle);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).GetFileAsync("File:Nonexistent.jpg", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(MediaWikiErrorKeys.NonexistentRevision)]
    [InlineData(MediaWikiErrorKeys.NoRevision)]
    public async Task GetFileAsync_NotFoundWithRevisionErrorKey_ThrowsMediaWikiException(string errorKey)
    {
        // A file endpoint takes no revision, so a key that reports one missing is not absence here.
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(errorKey);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetFileAsync("File:Example.jpg", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(errorKey, exception.ErrorKey);
    }

    [Fact]
    public async Task GetFileAsync_ReturnsFileWithItsRenditions()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(FileJson);
        using var httpClient = handler.CreateClient();

        var file = await new MediaWikiClient(httpClient).GetFileAsync("File:Fennec Fox.jpg", TestContext.Current.CancellationToken);

        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}file/File%3AFennec%20Fox.jpg", handler.Request.RequestUri?.AbsoluteUri);

        Assert.NotNull(file);
        Assert.Equal("Fennec Fox.jpg", file.Title);
        Assert.Equal("//commons.wikimedia.org/wiki/File:Fennec_Fox.jpg", file.FileDescriptionUrl);
        Assert.Equal(234501, file.Latest?.User?.Id);
        Assert.Equal(800, file.Preferred?.Width);
        Assert.Equal(4022330, file.Original?.Size);
        Assert.Equal(320, file.Thumbnail?.Width);
        Assert.Null(file.Thumbnail?.Duration);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetFileThumbnailsAsync_BlankTitle_ThrowsArgumentException(string title)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(ThumbnailsJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetFileThumbnailsAsync(title, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public async Task GetFileThumbnailsAsync_DotSegmentTitle_ThrowsArgumentException(string title)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(ThumbnailsJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => client.GetFileThumbnailsAsync(title, TestContext.Current.CancellationToken));

        Assert.Equal("title", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetFileThumbnailsAsync_FileDoesNotExist_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.CannotLoadFile);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).GetFileThumbnailsAsync("File:Nonexistent.jpg", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetFileThumbnailsAsync_FileNotThumbnailable_ReturnsNull()
    {
        // The wiki refuses audio, or a document it has no handler for, with 400 rather than 404.
        const string errorJson =
            $$"""
              {
                "errorKey": "{{MediaWikiErrorKeys.FileNotThumbnailable}}",
                "messageTranslations": { "en": "The file Song.ogg cannot be thumbnailed." },
                "httpCode": 400,
                "httpReason": "Bad Request"
              }
              """;
        using var handler = HttpMessageHandlerStub.CreateReturningJson(errorJson, HttpStatusCode.BadRequest);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).GetFileThumbnailsAsync("File:Song.ogg", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetFileThumbnailsAsync_FilePageDoesNotExist_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentTitle);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).GetFileThumbnailsAsync("File:Nonexistent.jpg", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetFileThumbnailsAsync_OtherBadRequest_ThrowsMediaWikiException()
    {
        const string errorJson =
            $$"""
              {
                "errorKey": "{{MediaWikiErrorKeys.BadTitle}}",
                "messageTranslations": { "en": "The title is not valid." },
                "httpCode": 400,
                "httpReason": "Bad Request"
              }
              """;
        using var handler = HttpMessageHandlerStub.CreateReturningJson(errorJson, HttpStatusCode.BadRequest);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetFileThumbnailsAsync("File:<>", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(MediaWikiErrorKeys.BadTitle, exception.ErrorKey);
    }

    [Fact]
    public async Task GetFileThumbnailsAsync_ReturnsThumbnails()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(ThumbnailsJson);
        using var httpClient = handler.CreateClient();

        var thumbnails = await new MediaWikiClient(httpClient).GetFileThumbnailsAsync("File:Fennec Fox.jpg", TestContext.Current.CancellationToken);

        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}file/File%3AFennec%20Fox.jpg/thumbnails", handler.Request.RequestUri?.AbsoluteUri);

        Assert.NotNull(thumbnails);
        Assert.Equal("Fennec Fox.jpg", thumbnails.Title);
        Assert.Equal(1200, thumbnails.Original.Width);
        Assert.Equal("BITMAP", thumbnails.Original.MediaType);

        Assert.Equal(2, thumbnails.Thumbnails.Count);
        Assert.Equal(120, thumbnails.Thumbnails[0].Width);
        Assert.Equal("image/jpeg", thumbnails.Thumbnails[0].MimeType);
        Assert.Equal(
            "https://example.org/w/images/thumb/a/ab/Fennec_Fox.jpg/240px-Fennec_Fox.jpg",
            thumbnails.Thumbnails[0].ResponsiveUrls?["2"]);

        // A wiki reports higher-density renderings only where it has them.
        Assert.Null(thumbnails.Thumbnails[1].ResponsiveUrls);
    }
}
