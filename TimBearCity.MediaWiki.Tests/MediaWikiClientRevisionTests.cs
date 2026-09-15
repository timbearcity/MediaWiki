using System.Net.Mime;
using System.Text.Json;
using TimBearCity.MediaWiki.Revisions;
using Xunit;

namespace TimBearCity.MediaWiki.Tests;

public sealed class MediaWikiClientRevisionTests
{
    private const string ComparisonJson =
        """
        {
          "from": {
            "id": 847170467,
            "slot_role": "main",
            "sections": [ { "level": 2, "heading": "==Description==", "offset": 3006 } ]
          },
          "to": {
            "id": 851733941,
            "slot_role": "main",
            "sections": []
          },
          "diff": [
            {
              "type": 0,
              "text": "A line both revisions share.",
              "offset": { "from": 10, "to": 10 },
              "lineNumber": 1
            },
            {
              "type": 3,
              "text": "A line they disagree on.",
              "offset": { "from": 39, "to": 39 },
              "lineNumber": 2,
              "highlightRanges": [ { "start": 2, "length": 4, "type": 1 } ]
            },
            {
              "type": 5,
              "text": "A line that moved.",
              "offset": { "from": null, "to": 63 },
              "moveInfo": { "id": "movedpara_2_5_lhs", "linkId": "movedpara_4_2_rhs", "linkDirection": 0 }
            }
          ]
        }
        """;

    private const string LintJson =
        """
        [
          {
            "type": "obsolete-tag",
            "dsr": [ 0, 7, 3, 4 ],
            "templateInfo": { "name": "Template:Infobox" },
            "params": { "name": "tt" }
          },
          {
            "type": "missing-end-tag",
            "dsr": [ 8, 20, 3, null ],
            "templateInfo": null,
            "params": []
          }
        ]
        """;

    private const string RevisionBareJson =
        """
        {
          "id": 1234567,
          "size": 42,
          "minor": false,
          "timestamp": "2024-01-02T03:04:05Z",
          "content_model": "wikitext",
          "page": { "id": 9228, "key": "Albert_Einstein", "title": "Albert Einstein" },
          "license": { "title": "CC BY-SA 4.0", "url": "https://creativecommons.org/licenses/by-sa/4.0/" },
          "user": { "id": 7903804, "name": "Citation bot" },
          "comment": "Added isbn.",
          "delta": 139,
          "html_url": "https://en.wikipedia.org/w/rest.php/v1/revision/1234567/html"
        }
        """;

    private const string RevisionJson =
        """
        {
          "id": 1234567,
          "size": 42,
          "minor": false,
          "timestamp": "2024-01-02T03:04:05Z",
          "content_model": "wikitext",
          "page": { "id": 9228, "key": "Albert_Einstein", "title": "Albert Einstein" },
          "license": { "title": "CC BY-SA 4.0", "url": "https://creativecommons.org/licenses/by-sa/4.0/" },
          "user": { "id": 7903804, "name": "Citation bot" },
          "comment": "Added isbn.",
          "delta": 139,
          "source": "Albert Einstein was a physicist."
        }
        """;

    /// <summary>An anonymous edit, which the wiki reports without a user identifier and without a summary.</summary>
    private const string RevisionWithAnonymousUserJson =
        """
        {
          "id": 1234567,
          "size": 42,
          "minor": true,
          "timestamp": "2024-01-02T03:04:05Z",
          "content_model": "wikitext",
          "page": { "id": 9228, "key": "Albert_Einstein", "title": "Albert Einstein" },
          "license": { "title": "CC BY-SA 4.0", "url": "https://creativecommons.org/licenses/by-sa/4.0/" },
          "user": { "id": null, "name": "203.0.113.7" },
          "comment": null,
          "delta": null,
          "source": "Albert Einstein was a physicist."
        }
        """;

    private const string RevisionWithHtmlJson =
        """
        {
          "id": 1234567,
          "size": 42,
          "minor": false,
          "timestamp": "2024-01-02T03:04:05Z",
          "content_model": "wikitext",
          "page": { "id": 9228, "key": "Albert_Einstein", "title": "Albert Einstein" },
          "license": { "title": "CC BY-SA 4.0", "url": "https://creativecommons.org/licenses/by-sa/4.0/" },
          "user": { "id": 7903804, "name": "Citation bot" },
          "comment": "Added isbn.",
          "delta": 139,
          "html": "<!DOCTYPE html><p>Albert Einstein was a physicist.</p>"
        }
        """;

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CompareRevisionsAsync_FromRevisionIdNotPositive_ThrowsArgumentOutOfRangeException(long fromRevisionId)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(ComparisonJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.CompareRevisionsAsync(fromRevisionId, 851733941, TestContext.Current.CancellationToken));

        Assert.Equal("fromRevisionId", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CompareRevisionsAsync_ReturnsComparison()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(ComparisonJson);
        using var httpClient = handler.CreateClient();

        var comparison = await new MediaWikiClient(httpClient).CompareRevisionsAsync(847170467, 851733941, TestContext.Current.CancellationToken);

        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}revision/847170467/compare/851733941", handler.Request.RequestUri?.AbsoluteUri);

        Assert.NotNull(comparison);
        Assert.Equal(847170467, comparison.From.Id);
        Assert.Equal("main", comparison.From.SlotRole);
        Assert.Equal("==Description==", Assert.Single(comparison.From.Sections).Heading);
        Assert.Empty(comparison.To.Sections);

        Assert.Equal(3, comparison.Diff.Count);

        Assert.Equal(MediaWikiDiffType.Context, comparison.Diff[0].Type);
        Assert.Equal(1, comparison.Diff[0].LineNumber);
        Assert.Equal(10, comparison.Diff[0].Offset.From);
        Assert.Null(comparison.Diff[0].HighlightRanges);
        Assert.Null(comparison.Diff[0].MoveInfo);

        Assert.Equal(MediaWikiDiffType.Changed, comparison.Diff[1].Type);
        var highlight = Assert.Single(comparison.Diff[1].HighlightRanges!);
        Assert.Equal(2, highlight.Start);
        Assert.Equal(4, highlight.Length);
        Assert.Equal(MediaWikiDiffHighlightType.Deletion, highlight.Type);

        Assert.Equal(MediaWikiDiffType.MovedTo, comparison.Diff[2].Type);
        Assert.Null(comparison.Diff[2].Offset.From);
        Assert.Equal(63, comparison.Diff[2].Offset.To);
        var moveInfo = comparison.Diff[2].MoveInfo;
        Assert.NotNull(moveInfo);
        Assert.Equal("movedpara_2_5_lhs", moveInfo.Id);
        Assert.Equal("movedpara_4_2_rhs", moveInfo.LinkId);
        Assert.Equal(MediaWikiDiffMoveDirection.Lower, moveInfo.LinkDirection);
    }

    [Fact]
    public async Task CompareRevisionsAsync_RevisionDoesNotExist_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.CompareNonexistent);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).CompareRevisionsAsync(847170467, 851733941, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CompareRevisionsAsync_ToRevisionIdNotPositive_ThrowsArgumentOutOfRangeException(long toRevisionId)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(ComparisonJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.CompareRevisionsAsync(847170467, toRevisionId, TestContext.Current.CancellationToken));

        Assert.Equal("toRevisionId", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetRevisionAsync_AnonymousEdit_ReportsAddressWithoutIdentifier()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(RevisionWithAnonymousUserJson);
        using var httpClient = handler.CreateClient();

        var revision = await new MediaWikiClient(httpClient).GetRevisionAsync(1234567, TestContext.Current.CancellationToken);

        Assert.NotNull(revision);
        Assert.NotNull(revision.User);
        Assert.Null(revision.User.Id);
        Assert.Equal("203.0.113.7", revision.User.Name);
        Assert.Null(revision.Comment);
        Assert.Null(revision.Delta);
        Assert.True(revision.IsMinor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetRevisionAsync_IdNotPositive_ThrowsArgumentOutOfRangeException(long id)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(RevisionJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetRevisionAsync(id, TestContext.Current.CancellationToken));

        Assert.Equal("id", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetRevisionAsync_ReturnsRevision()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(RevisionJson);
        using var httpClient = handler.CreateClient();

        var revision = await new MediaWikiClient(httpClient).GetRevisionAsync(1234567, TestContext.Current.CancellationToken);

        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}revision/1234567", handler.Request.RequestUri?.AbsoluteUri);

        Assert.NotNull(revision);
        Assert.Equal(1234567, revision.Id);
        Assert.Equal(42, revision.Size);
        Assert.Equal(139, revision.Delta);
        Assert.Equal("Added isbn.", revision.Comment);
        Assert.Equal("Albert Einstein was a physicist.", revision.Source);
        Assert.Equal("Albert_Einstein", revision.Page.Key);
        Assert.Equal(7903804, revision.User?.Id);
        Assert.Equal("CC BY-SA 4.0", revision.License.Title);
    }

    [Fact]
    public async Task GetRevisionAsync_RevisionDoesNotExist_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentRevision);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).GetRevisionAsync(1234567, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetRevisionBareAsync_IdNotPositive_ThrowsArgumentOutOfRangeException(long id)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(RevisionBareJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetRevisionBareAsync(id, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetRevisionBareAsync_ReturnsMetadataAndHtmlUrl()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(RevisionBareJson);
        using var httpClient = handler.CreateClient();

        var revision = await new MediaWikiClient(httpClient).GetRevisionBareAsync(1234567, TestContext.Current.CancellationToken);

        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}revision/1234567/bare", handler.Request.RequestUri?.AbsoluteUri);

        Assert.NotNull(revision);
        Assert.Equal("https://en.wikipedia.org/w/rest.php/v1/revision/1234567/html", revision.HtmlUrl);
        Assert.Equal("wikitext", revision.ContentModel);
    }

    [Fact]
    public async Task GetRevisionBareAsync_RevisionDoesNotExist_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentRevision);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).GetRevisionBareAsync(1234567, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetRevisionHtmlAsync_IdNotPositive_ThrowsArgumentOutOfRangeException(long id)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningContent("<p>x</p>", MediaTypeNames.Text.Html);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetRevisionHtmlAsync(id, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetRevisionHtmlAsync_ReturnsHtml()
    {
        const string html = "<!DOCTYPE html><p>Albert Einstein was a physicist.</p>";

        using var handler = HttpMessageHandlerStub.CreateReturningContent(html, MediaTypeNames.Text.Html);
        using var httpClient = handler.CreateClient();

        var htmlResponse = await new MediaWikiClient(httpClient).GetRevisionHtmlAsync(1234567, TestContext.Current.CancellationToken);

        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}revision/1234567/html", handler.Request.RequestUri?.AbsoluteUri);
        Assert.Equal(MediaTypeNames.Text.Html, Assert.Single(handler.Request.Headers.Accept).MediaType);
        Assert.Equal(html, htmlResponse);
    }

    [Fact]
    public async Task GetRevisionHtmlAsync_RevisionDoesNotExist_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentRevision);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).GetRevisionHtmlAsync(1234567, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetRevisionLintAsync_IdNotPositive_ThrowsArgumentOutOfRangeException(long id)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(LintJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetRevisionLintAsync(id, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetRevisionLintAsync_ReturnsLintErrors()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(LintJson);
        using var httpClient = handler.CreateClient();

        var errors = await new MediaWikiClient(httpClient).GetRevisionLintAsync(1234567, TestContext.Current.CancellationToken);

        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}revision/1234567/lint", handler.Request.RequestUri?.AbsoluteUri);

        Assert.NotNull(errors);
        Assert.Equal(2, errors.Count);

        Assert.Equal("obsolete-tag", errors[0].Type);
        Assert.Equal([0, 7, 3, 4], errors[0].Dsr);
        Assert.Equal("Template:Infobox", errors[0].TemplateInfo?.Name);
        Assert.Equal("tt", errors[0].Params.GetProperty("name").GetString());

        // The linter leaves out what it could not measure, and reports "no detail" as an empty array rather than an object.
        Assert.Null(errors[1].Dsr[3]);
        Assert.Null(errors[1].TemplateInfo);
        Assert.Equal(JsonValueKind.Array, errors[1].Params.ValueKind);
    }

    [Fact]
    public async Task GetRevisionLintAsync_RevisionDoesNotExist_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentRevision);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).GetRevisionLintAsync(1234567, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetRevisionWithHtmlAsync_IdNotPositive_ThrowsArgumentOutOfRangeException(long id)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(RevisionWithHtmlJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetRevisionWithHtmlAsync(id, TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetRevisionWithHtmlAsync_ReturnsMetadataAndHtml()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(RevisionWithHtmlJson);
        using var httpClient = handler.CreateClient();

        var revision = await new MediaWikiClient(httpClient).GetRevisionWithHtmlAsync(1234567, TestContext.Current.CancellationToken);

        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}revision/1234567/with_html", handler.Request.RequestUri?.AbsoluteUri);

        Assert.NotNull(revision);
        Assert.Equal("<!DOCTYPE html><p>Albert Einstein was a physicist.</p>", revision.Html);
        Assert.Equal(9228, revision.Page.Id);
    }

    [Fact]
    public async Task GetRevisionWithHtmlAsync_RevisionDoesNotExist_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentRevision);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).GetRevisionWithHtmlAsync(1234567, TestContext.Current.CancellationToken));
    }
}
