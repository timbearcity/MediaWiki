using System.Net;
using System.Net.Mime;
using System.Reflection;
using System.Text;
using TimBearCity.MediaWiki.Pages;
using Xunit;

namespace TimBearCity.MediaWiki.Tests;

public sealed class MediaWikiClientPageHistoryTests
{
    private const string HistoryCountJson = """{ "count": 110, "limit": false }""";

    private const string HistoryJson =
        """
        {
          "revisions": [
            {
              "id": 1218700625,
              "timestamp": "2024-04-13T08:05:03Z",
              "minor": false,
              "size": 195291,
              "comment": "Alter: date, template type",
              "user": { "id": 7903804, "name": "Citation bot" },
              "delta": 139
            },
            {
              "id": 1218700624,
              "timestamp": "2024-04-12T08:05:03Z",
              "minor": true,
              "size": 195152,
              "comment": null,
              "user": null,
              "delta": null
            }
          ],
          "latest": "https://en.wikipedia.org/w/rest.php/v1/page/Solar_System/history",
          "older": "https://en.wikipedia.org/w/rest.php/v1/page/Solar_System/history?older_than=1218700625"
        }
        """;

    private const string PlaceholderRejectedJson =
        """
        {
          "error": "parameter-validation-failed",
          "name": "type",
          "value": "{type}",
          "failureCode": "badvalue",
          "errorKey": "badvalue",
          "messageTranslations": { "en": "Unrecognized value for parameter \"type\": {type}." },
          "httpCode": 400,
          "httpReason": "Bad Request"
        }
        """;

    [Theory]
    [InlineData(null, null, null, "")]
    [InlineData(null, null, MediaWikiPageHistoryFilter.Anonymous, "?filter=anonymous")]
    [InlineData(null, null, MediaWikiPageHistoryFilter.Bot, "?filter=bot")]
    [InlineData(null, null, MediaWikiPageHistoryFilter.Reverted, "?filter=reverted")]
    [InlineData(null, null, MediaWikiPageHistoryFilter.Minor, "?filter=minor")]
    [InlineData(1218700625L, 1218700000L, MediaWikiPageHistoryFilter.Bot, "?older_than=1218700625&newer_than=1218700000&filter=bot")]
    public async Task GetPageHistoryAsync_Arguments_RequestsMatchingQuery(
        long? olderThan,
        long? newerThan,
        MediaWikiPageHistoryFilter? filter,
        string expectedQuery)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(HistoryJson);
        using var httpClient = handler.CreateClient();

        await new MediaWikiClient(httpClient).GetPageHistoryAsync("Solar System", olderThan, newerThan, filter, TestContext.Current.CancellationToken);

        Assert.Equal(
            $"{HttpMessageHandlerStub.DefaultBaseAddress}page/Solar_System/history{expectedQuery}",
            handler.Request.RequestUri?.AbsoluteUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPageHistoryAsync_BlankKey_ThrowsArgumentException(string key)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(HistoryJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetPageHistoryAsync(key, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetPageHistoryAsync_MalformedKey_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.InvalidTitle);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).GetPageHistoryAsync("<>", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetPageHistoryAsync_NewerThanNotPositive_ThrowsArgumentOutOfRangeException(long newerThan)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(HistoryJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.GetPageHistoryAsync("Solar_System", newerThan: newerThan, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("newerThan", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetPageHistoryAsync_OlderThanNotPositive_ThrowsArgumentOutOfRangeException(long olderThan)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(HistoryJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.GetPageHistoryAsync("Solar_System", olderThan, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("olderThan", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetPageHistoryAsync_PageDoesNotExist_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentTitle);
        using var httpClient = handler.CreateClient();

        Assert.Null(await new MediaWikiClient(httpClient).GetPageHistoryAsync("Nonexistent", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetPageHistoryAsync_ReturnsRevisions()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(HistoryJson);
        using var httpClient = handler.CreateClient();

        var history = await new MediaWikiClient(httpClient).GetPageHistoryAsync("Solar_System", cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(history);
        Assert.Equal(2, history.Revisions.Count);
        Assert.Equal(1218700625, history.Revisions[0].Id);
        Assert.Equal("Citation bot", history.Revisions[0].User?.Name);
        Assert.Equal(139, history.Revisions[0].Delta);

        // The oldest revision on the wiki has nothing to measure against, and the wiki may withhold the author.
        Assert.Null(history.Revisions[1].Delta);
        Assert.Null(history.Revisions[1].User);
        Assert.Null(history.Revisions[1].Comment);
        Assert.True(history.Revisions[1].IsMinor);

        Assert.EndsWith("history", history.Latest, StringComparison.Ordinal);
        Assert.EndsWith("older_than=1218700625", history.Older, StringComparison.Ordinal);
        Assert.Null(history.Newer);
    }

    [Fact]
    public async Task GetPageHistoryAsync_RevisionNotOfPage_ThrowsMediaWikiException()
    {
        // The wiki answers this way both for a revision of another page and for one that does not exist at all.
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentTitleRevision);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() =>
            client.GetPageHistoryAsync("Solar_System", 1374702991, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(MediaWikiErrorKeys.NonexistentTitleRevision, exception.ErrorKey);
    }

    [Fact]
    public async Task GetPageHistoryAsync_UnknownFilter_ThrowsArgumentOutOfRangeException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(HistoryJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetPageHistoryAsync(
            "Solar_System",
            filter: (MediaWikiPageHistoryFilter)99,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("filter", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(MediaWikiPageHistoryCountType.Anonymous, null, null, "anonymous")]
    [InlineData(MediaWikiPageHistoryCountType.Temporary, null, null, "temporary")]
    [InlineData(MediaWikiPageHistoryCountType.Bot, null, null, "bot")]
    [InlineData(MediaWikiPageHistoryCountType.Editors, null, null, "editors")]
    [InlineData(MediaWikiPageHistoryCountType.Edits, null, null, "edits")]
    [InlineData(MediaWikiPageHistoryCountType.Minor, null, null, "minor")]
    [InlineData(MediaWikiPageHistoryCountType.Reverted, null, null, "reverted")]
#pragma warning disable CS0618
    [InlineData(MediaWikiPageHistoryCountType.AnonymousEdits, null, null, "anonedits")]
    [InlineData(MediaWikiPageHistoryCountType.BotEdits, null, null, "botedits")]
    [InlineData(MediaWikiPageHistoryCountType.RevertedEdits, null, null, "revertededits")]
    [InlineData(MediaWikiPageHistoryCountType.BotEdits, 1218700000L, 1218700625L, "botedits?from=1218700000&to=1218700625")]
#pragma warning restore CS0618
    public async Task GetPageHistoryCountAsync_Arguments_RequestsMatchingPath(
        MediaWikiPageHistoryCountType type,
        long? fromRevisionId,
        long? toRevisionId,
        string expectedPath)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(HistoryCountJson);
        using var httpClient = handler.CreateClient();

        await new MediaWikiClient(httpClient).GetPageHistoryCountAsync("Solar_System", type, fromRevisionId, toRevisionId,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            $"{HttpMessageHandlerStub.DefaultBaseAddress}page/Solar_System/history/counts/{expectedPath}",
            handler.Request.RequestUri?.AbsoluteUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("page/Albert_Einstein/history/counts/edits")]
    [InlineData("https://proxy.example/w/rest.php/v1/page/Albert_Einstein/history/counts/{type}")]
    public async Task GetPageHistoryCountAsync_BadRequestNotFromPlaceholder_ThrowsWithoutRepeating(string? landedUri)
    {
        // A 400 is repeated only when the response says it landed on a placeholder under the base address: a handler that
        // reports no request, one that answered the request as sent, and a redirect that led off the wiki are all reported as they are.
        using var handler = HttpMessageHandlerStub.CreateResponding(request =>
        {
            if (landedUri is not null)
            {
                request.RequestUri = new Uri(new Uri(HttpMessageHandlerStub.DefaultBaseAddress), landedUri);
            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(PlaceholderRejectedJson, Encoding.UTF8, MediaTypeNames.Application.Json),
                RequestMessage = landedUri is null ? null : request
            };
        });
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageHistoryCountAsync(
            "Albert_Einstein",
            MediaWikiPageHistoryCountType.Edits,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Single(handler.Hops);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPageHistoryCountAsync_BlankKey_ThrowsArgumentException(string key)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(HistoryCountJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.GetPageHistoryCountAsync(key, MediaWikiPageHistoryCountType.Edits, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetPageHistoryCountAsync_FromRevisionIdNotPositive_ThrowsArgumentOutOfRangeException(long fromRevisionId)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(HistoryCountJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetPageHistoryCountAsync(
            "Solar_System",
            MediaWikiPageHistoryCountType.Edits,
            fromRevisionId,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("fromRevisionId", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetPageHistoryCountAsync_KeyWithSpaces_RequestsUnderscoredKey()
    {
        // The stored key form lands directly, sparing the round trip through the wiki's 301 to the normalized title.
        using var handler = HttpMessageHandlerStub.CreateReturningJson(HistoryCountJson);
        using var httpClient = handler.CreateClient();

        await new MediaWikiClient(httpClient).GetPageHistoryCountAsync("Solar System", MediaWikiPageHistoryCountType.Edits,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            $"{HttpMessageHandlerStub.DefaultBaseAddress}page/Solar_System/history/counts/edits",
            handler.Request.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task GetPageHistoryCountAsync_MalformedKey_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.InvalidTitle);
        using var httpClient = handler.CreateClient();

        var count = await new MediaWikiClient(httpClient).GetPageHistoryCountAsync(
            "<>",
            MediaWikiPageHistoryCountType.Edits,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(count);
    }

    [Fact]
    public async Task GetPageHistoryCountAsync_PageDoesNotExist_ReturnsNull()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentTitle);
        using var httpClient = handler.CreateClient();

        var count = await new MediaWikiClient(httpClient).GetPageHistoryCountAsync(
            "Nonexistent",
            MediaWikiPageHistoryCountType.Edits,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(count);
    }

    [Fact]
    public async Task GetPageHistoryCountAsync_RedirectFollowedOntoPlaceholder_RepeatsWithTypeFilledIn()
    {
        // MediaWiki 1.43 through 1.45 redirect a title that needs normalizing to a counts path with {type} left unsubstituted, and
        // a plain HttpClient follows it before the client can repair it, so the 400 the wiki answers with is what the client sees.
        const string normalizedPath = "page/Albert_Einstein/history/counts/{type}?from=1218700000&to=1218700625";
        var first = true;

        using var handler = HttpMessageHandlerStub.CreateResponding(request =>
        {
            if (!first)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(HistoryCountJson, Encoding.UTF8, MediaTypeNames.Application.Json)
                };
            }

            first = false;
            request.RequestUri = new Uri(HttpMessageHandlerStub.DefaultBaseAddress + normalizedPath);

            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(PlaceholderRejectedJson, Encoding.UTF8, MediaTypeNames.Application.Json),
                RequestMessage = request
            };
        });
        using var httpClient = handler.CreateClient();

        var count = await new MediaWikiClient(httpClient).GetPageHistoryCountAsync(
            "albert_Einstein",
            MediaWikiPageHistoryCountType.Edits,
            1218700000,
            1218700625,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                $"{HttpMessageHandlerStub.DefaultBaseAddress}page/albert_Einstein/history/counts/edits?from=1218700000&to=1218700625",
                $"{HttpMessageHandlerStub.DefaultBaseAddress}page/Albert_Einstein/history/counts/edits?from=1218700000&to=1218700625"
            ],
            handler.Hops.Select(hop => hop.Uri));
        Assert.Equal(110, count?.Count);
    }

    [Fact]
    public async Task GetPageHistoryCountAsync_ReturnsCount()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(HistoryCountJson);
        using var httpClient = handler.CreateClient();

        var count = await new MediaWikiClient(httpClient).GetPageHistoryCountAsync(
            "Solar_System",
            MediaWikiPageHistoryCountType.Edits,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(count);
        Assert.Equal(110, count.Count);
        Assert.False(count.IsLimitExceeded);
    }

    [Fact]
    public async Task GetPageHistoryCountAsync_RevisionDoesNotExist_ThrowsMediaWikiException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningNotFound(MediaWikiErrorKeys.NonexistentRevision);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageHistoryCountAsync(
            "Solar_System",
            MediaWikiPageHistoryCountType.Edits,
            1,
            2,
            TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(MediaWikiErrorKeys.NonexistentRevision, exception.ErrorKey);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetPageHistoryCountAsync_ToRevisionIdNotPositive_ThrowsArgumentOutOfRangeException(long toRevisionId)
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(HistoryCountJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetPageHistoryCountAsync(
            "Solar_System",
            MediaWikiPageHistoryCountType.Edits,
            toRevisionId: toRevisionId,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("toRevisionId", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetPageHistoryCountAsync_UnknownType_ThrowsArgumentOutOfRangeException()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(HistoryCountJson);
        using var httpClient = handler.CreateClient();
        var client = new MediaWikiClient(httpClient);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetPageHistoryCountAsync(
            "Solar_System",
            (MediaWikiPageHistoryCountType)99,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("type", exception.ParamName);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("AnonymousEdits", nameof(MediaWikiPageHistoryCountType.Anonymous))]
    [InlineData("BotEdits", nameof(MediaWikiPageHistoryCountType.Bot))]
    [InlineData("RevertedEdits", nameof(MediaWikiPageHistoryCountType.Reverted))]
    public void MediaWikiPageHistoryCountType_DeprecatedAlias_IsObsoleteNamingReplacement(string alias, string replacement)
    {
        var attribute = typeof(MediaWikiPageHistoryCountType).GetField(alias)?.GetCustomAttribute<ObsoleteAttribute>();

        Assert.NotNull(attribute);
        Assert.StartsWith($"Use {replacement} instead;", attribute.Message, StringComparison.Ordinal);
    }
}
