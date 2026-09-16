using System.Net;
using TimBearCity.MediaWiki.Pages;
using Xunit;

namespace TimBearCity.MediaWiki.Tests;

/// <summary>
/// Read-only calls against a live wiki, proving the record shapes against real responses rather than the canned
/// ones the rest of the suite uses.
/// </summary>
/// <remarks>
/// <para>
/// Opt in by setting <c>MEDIAWIKI_SMOKE_READ_BASE_URL</c> to a REST API root such as
/// <c>https://en.wikipedia.org/w/rest.php/v1/</c>; without it every test here is skipped. <c>MEDIAWIKI_SMOKE_READ_PAGE</c>
/// and <c>MEDIAWIKI_SMOKE_READ_FILE</c> pick the page and file to read, and default to ones that exist on the English
/// Wikipedia. The page needs at least two revisions for the comparison test, and more than a page of history for the
/// pagination test; a wiki without them skips those. A wiki whose MediaWiki version predates an endpoint skips the test
/// for it, instead of failing on <c>rest-no-match</c>.
/// </para>
/// <para>
/// Only stable properties are asserted: ids match across endpoints, lists are well-formed, text is non-empty. The
/// refusals the documentation promises are asserted too, since the status code and error key a wiki really answers
/// with are exactly what canned responses cannot vouch for. Nothing here writes to the wiki.
/// </para>
/// </remarks>
public sealed class MediaWikiClientReadSmokeTests(ReadableWikiFixture wiki) : IClassFixture<ReadableWikiFixture>
{
    /// <summary>A revision no wiki has reached, yet small enough for the wiki to parse rather than refuse.</summary>
    private const long MissingRevisionId = int.MaxValue;

    private const string NotConfigured = "Set MEDIAWIKI_SMOKE_READ_BASE_URL to the REST API root of a wiki to run the read smoke tests.";

    public static bool IsEnabled => ReadableWikiFixture.BaseUrl is not null;

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task CompareRevisionsAsync_LiveRevisions_ReturnsLineBasedDiff()
    {
        var history = await wiki.Client.GetPageHistoryAsync(wiki.Page, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(history);
        Assert.SkipWhen(history.Revisions.Count < 2, $"Page \"{wiki.Page}\" has a single revision, so there is nothing to compare.");

        var to = history.Revisions[0];
        var from = history.Revisions[1];

        var comparison = await wiki.Client.CompareRevisionsAsync(from.Id, to.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(comparison);
        Assert.Equal(from.Id, comparison.From.Id);
        Assert.Equal(to.Id, comparison.To.Id);
        Assert.NotEmpty(comparison.From.SlotRole);
        Assert.All(comparison.Diff, entry => Assert.True(Enum.IsDefined(entry.Type), $"Unknown diff entry type {entry.Type}."));
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task CompareRevisionsAsync_MissingRevision_ReturnsNull()
    {
        var page = await GetPageBareAsync();

        var comparison = await wiki.Client.CompareRevisionsAsync(MissingRevisionId, page.Latest.Id, TestContext.Current.CancellationToken);

        Assert.Null(comparison);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task CompareRevisionsAsync_RevisionsOfDifferentPages_ThrowsBadRequest()
    {
        var page = await GetPageBareAsync();
        var other = await FindAnotherPageAsync(page);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() =>
            wiki.Client.CompareRevisionsAsync(other.Latest.Id, page.Latest.Id, TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetFileAsync_LiveFile_ReturnsDescriptionAndOriginal()
    {
        var file = await wiki.Client.GetFileAsync(wiki.File, TestContext.Current.CancellationToken);

        Assert.NotNull(file);
        Assert.EndsWith(file.Title, wiki.File, StringComparison.Ordinal);
        Assert.NotEmpty(file.FileDescriptionUrl);
        Assert.NotNull(file.Original);
        Assert.NotEmpty(file.Original.Url);
        Assert.NotEmpty(file.Original.MediaType);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetFileAsync_MissingFile_ReturnsNull()
    {
        var file = await wiki.Client.GetFileAsync($"File:{Guid.NewGuid():N}.png", TestContext.Current.CancellationToken);

        Assert.Null(file);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetFileThumbnailsAsync_LiveFile_ReturnsOriginalAndThumbnails()
    {
        var thumbnails = await SkipUnlessServedAsync(() => wiki.Client.GetFileThumbnailsAsync(wiki.File, TestContext.Current.CancellationToken));

        Assert.NotNull(thumbnails);
        Assert.EndsWith(thumbnails.Title, wiki.File, StringComparison.Ordinal);
        Assert.NotEmpty(thumbnails.Original.Url);
        Assert.All(thumbnails.Thumbnails, thumbnail =>
        {
            Assert.NotEmpty(thumbnail.Url);
            Assert.NotEmpty(thumbnail.MimeType);
            Assert.True(thumbnail.Width > 0);
            Assert.True(thumbnail.Height > 0);
        });
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetFileThumbnailsAsync_MissingFile_ReturnsNull()
    {
        var thumbnails = await SkipUnlessServedAsync(() =>
            wiki.Client.GetFileThumbnailsAsync($"File:{Guid.NewGuid():N}.png", TestContext.Current.CancellationToken));

        Assert.Null(thumbnails);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageAsync_LivePage_ReturnsSource()
    {
        var page = await wiki.Client.GetPageAsync(wiki.Page, TestContext.Current.CancellationToken);

        Assert.NotNull(page);
        Assert.Equal(wiki.Page, page.Key);
        Assert.True(page.Id > 0);
        Assert.True(page.Latest.Id > 0);
        Assert.NotEmpty(page.ContentModel);
        Assert.NotEmpty(page.Source);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageAsync_MediaWikiNamespacePage_ReturnsPage()
    {
        // Wikimedia wikis answer this page with a placeholder revision (id 0, timestamp null); a wiki that has never saved it answers 404.
        var page = await wiki.Client.GetPageAsync("MediaWiki:Common.css", TestContext.Current.CancellationToken);

        Assert.SkipWhen(page is null, "The wiki has no MediaWiki:Common.css page, so there is nothing to read.");
        Assert.Equal("MediaWiki:Common.css", page.Key);
        Assert.NotEmpty(page.ContentModel);
        Assert.NotEmpty(page.Source);
        if (page.Latest.Id == 0)
        {
            Assert.Null(page.Latest.Timestamp);
        }
        else
        {
            Assert.NotNull(page.Latest.Timestamp);
        }
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageAsync_MissingPage_ReturnsNull()
    {
        var page = await wiki.Client.GetPageAsync(BuildMissingKey(), TestContext.Current.CancellationToken);

        Assert.Null(page);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageBareAsync_LivePage_ReturnsMetadata()
    {
        var page = await wiki.Client.GetPageBareAsync(wiki.Page, TestContext.Current.CancellationToken);

        Assert.NotNull(page);
        Assert.Equal(wiki.Page, page.Key);
        Assert.True(page.Id > 0);
        Assert.True(page.Latest.Id > 0);
        Assert.NotEmpty(page.ContentModel);
        Assert.NotEmpty(page.HtmlUrl);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageBareAsync_MalformedKey_ReturnsNull()
    {
        var page = await wiki.Client.GetPageBareAsync("<>", TestContext.Current.CancellationToken);

        Assert.Null(page);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageBareAsync_MissingPage_ReturnsNull()
    {
        var page = await wiki.Client.GetPageBareAsync(BuildMissingKey(), TestContext.Current.CancellationToken);

        Assert.Null(page);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageFilesAsync_LivePage_ListsFilesTheFileEndpointServes()
    {
        var files = await wiki.Client.GetPageFilesAsync(wiki.Page, TestContext.Current.CancellationToken);

        Assert.NotNull(files);
        Assert.SkipWhen(files.Count == 0, $"Page \"{wiki.Page}\" uses no files, so there is nothing to look up.");

        var file = await wiki.Client.GetFileAsync(files[0].Title, TestContext.Current.CancellationToken);

        Assert.NotNull(file);
        Assert.Equal(files[0].Title, file.Title);
        Assert.Equal(files[0].FileDescriptionUrl, file.FileDescriptionUrl);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageFilesAsync_LivePage_ReturnsWellFormedFiles()
    {
        var files = await wiki.Client.GetPageFilesAsync(wiki.Page, TestContext.Current.CancellationToken);

        Assert.NotNull(files);
        Assert.All(files, file =>
        {
            Assert.NotEmpty(file.Title);
            Assert.NotEmpty(file.FileDescriptionUrl);
        });
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageFilesAsync_MissingPage_ReturnsNull()
    {
        var files = await wiki.Client.GetPageFilesAsync(BuildMissingKey(), TestContext.Current.CancellationToken);

        Assert.Null(files);
    }

    [Theory(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    [InlineData(MediaWikiPageHistoryFilter.Anonymous)]
    [InlineData(MediaWikiPageHistoryFilter.Bot)]
    [InlineData(MediaWikiPageHistoryFilter.Reverted)]
    [InlineData(MediaWikiPageHistoryFilter.Minor)]
    public async Task GetPageHistoryAsync_EachFilter_ReturnsWellFormedHistory(MediaWikiPageHistoryFilter filter)
    {
        var history = await wiki.Client.GetPageHistoryAsync(wiki.Page, filter: filter, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(history);
        Assert.NotEmpty(history.Latest);
        Assert.All(history.Revisions, revision => Assert.True(revision.Id > 0));

        if (filter == MediaWikiPageHistoryFilter.Minor)
        {
            // The only filter the record itself can vouch for.
            Assert.All(history.Revisions, revision => Assert.True(revision.IsMinor));
        }
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageHistoryAsync_LivePage_ReturnsNewestFirst()
    {
        var history = await wiki.Client.GetPageHistoryAsync(wiki.Page, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(history);
        Assert.NotEmpty(history.Revisions);
        Assert.NotEmpty(history.Latest);
        Assert.All(history.Revisions, revision =>
        {
            Assert.True(revision.Id > 0);
            Assert.True(revision.Size >= 0);
        });

        var timestamps = history.Revisions.Select(revision => revision.Timestamp).ToList();
        Assert.Equal(timestamps.OrderByDescending(timestamp => timestamp), timestamps);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageHistoryAsync_LivePage_StartsAtLatestRevision()
    {
        var page = await GetPageBareAsync();

        var history = await wiki.Client.GetPageHistoryAsync(wiki.Page, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(history);
        Assert.Equal(page.Latest.Id, history.Revisions[0].Id);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageHistoryAsync_MissingPage_ReturnsNull()
    {
        var history = await wiki.Client.GetPageHistoryAsync(BuildMissingKey(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(history);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageHistoryAsync_NewerThanOldestListed_ReturnsNewerOnly()
    {
        var newest = await wiki.Client.GetPageHistoryAsync(wiki.Page, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(newest);
        Assert.SkipWhen(newest.Revisions.Count < 2, $"Page \"{wiki.Page}\" has a single revision, so nothing is newer than another.");

        var oldestListed = newest.Revisions[^1].Id;

        var newer = await wiki.Client.GetPageHistoryAsync(wiki.Page, newerThan: oldestListed, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(newer);
        Assert.NotEmpty(newer.Revisions);
        Assert.All(newer.Revisions, revision => Assert.True(revision.Id > oldestListed));
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageHistoryAsync_OlderThanAndNewerThan_ThrowsBadRequest()
    {
        var page = await GetPageBareAsync();

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() =>
            wiki.Client.GetPageHistoryAsync(wiki.Page, page.Latest.Id, page.Latest.Id, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(MediaWikiErrorKeys.PageHistoryIncompatibleParameters, exception.ErrorKey);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageHistoryAsync_OlderThanMissingRevision_ThrowsNotFound()
    {
        var exception = await Assert.ThrowsAsync<MediaWikiException>(() =>
            wiki.Client.GetPageHistoryAsync(wiki.Page, MissingRevisionId, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(MediaWikiErrorKeys.NonexistentTitleRevision, exception.ErrorKey);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageHistoryAsync_OlderThanNewest_ContinuesFromThere()
    {
        var newest = await wiki.Client.GetPageHistoryAsync(wiki.Page, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(newest);
        Assert.SkipWhen(newest.Older is null, $"Page \"{wiki.Page}\" fits in one page of history, so there is nothing older to fetch.");

        var older = await wiki.Client.GetPageHistoryAsync(wiki.Page, newest.Revisions[^1].Id, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(older);
        Assert.NotEmpty(older.Revisions);
        Assert.NotNull(older.Newer);
        Assert.True(older.Revisions[0].Id < newest.Revisions[^1].Id);
    }

    [Theory(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    [InlineData(MediaWikiPageHistoryCountType.Anonymous)]
    [InlineData(MediaWikiPageHistoryCountType.Temporary)]
    [InlineData(MediaWikiPageHistoryCountType.Bot)]
    [InlineData(MediaWikiPageHistoryCountType.Editors)]
    [InlineData(MediaWikiPageHistoryCountType.Edits)]
    [InlineData(MediaWikiPageHistoryCountType.Minor)]
    [InlineData(MediaWikiPageHistoryCountType.Reverted)]
    [InlineData(MediaWikiPageHistoryCountType.AnonymousEdits)]
    [InlineData(MediaWikiPageHistoryCountType.BotEdits)]
    [InlineData(MediaWikiPageHistoryCountType.RevertedEdits)]
    public async Task GetPageHistoryCountAsync_EachType_ReturnsCount(MediaWikiPageHistoryCountType type)
    {
        MediaWikiPageHistoryCount? count;
        try
        {
            count = await wiki.Client.GetPageHistoryCountAsync(wiki.Page, type, cancellationToken: TestContext.Current.CancellationToken);
        }
        catch (MediaWikiException exception) when (exception.ErrorKey == MediaWikiErrorKeys.PageHistoryCountTooManyRevisions)
        {
            // The minor count is the one type the wiki refuses outright, with a 500, for a page of more than 2000 edits.
            Assert.Skip($"Page \"{wiki.Page}\" has too many revisions for the wiki to count its {type} revisions.");
            throw;
        }

        Assert.NotNull(count);
        Assert.True(count.Count >= 0);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageHistoryCountAsync_EditsBetweenRevisions_CountsRange()
    {
        var history = await wiki.Client.GetPageHistoryAsync(wiki.Page, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(history);
        Assert.SkipWhen(history.Revisions.Count < 2, $"Page \"{wiki.Page}\" has a single revision, so there is no range to count.");

        var count = await wiki.Client.GetPageHistoryCountAsync(
            wiki.Page,
            MediaWikiPageHistoryCountType.Edits,
            history.Revisions[^1].Id,
            history.Revisions[0].Id,
            TestContext.Current.CancellationToken);

        Assert.NotNull(count);
        Assert.InRange(count.Count, 1, history.Revisions.Count);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageHistoryCountAsync_LivePage_CountsEdits()
    {
        var count = await wiki.Client.GetPageHistoryCountAsync(wiki.Page, MediaWikiPageHistoryCountType.Edits,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(count);
        Assert.True(count.Count > 0);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageHistoryCountAsync_MissingPage_ReturnsNull()
    {
        var count = await wiki.Client.GetPageHistoryCountAsync(BuildMissingKey(), MediaWikiPageHistoryCountType.Edits,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(count);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageHistoryCountAsync_OneEndOfRange_ThrowsBadRequest()
    {
        var page = await GetPageBareAsync();

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => wiki.Client.GetPageHistoryCountAsync(
            wiki.Page,
            MediaWikiPageHistoryCountType.Edits,
            page.Latest.Id,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(MediaWikiErrorKeys.PageHistoryCountParametersInvalid, exception.ErrorKey);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageHistoryCountAsync_RangeFromMissingRevision_ThrowsNotFound()
    {
        var page = await GetPageBareAsync();

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => wiki.Client.GetPageHistoryCountAsync(
            wiki.Page,
            MediaWikiPageHistoryCountType.Edits,
            MissingRevisionId,
            page.Latest.Id,
            TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(MediaWikiErrorKeys.NonexistentRevision, exception.ErrorKey);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageHistoryCountAsync_RangeOnMinor_ThrowsBadRequest()
    {
        var page = await GetPageBareAsync();

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => wiki.Client.GetPageHistoryCountAsync(
            wiki.Page,
            MediaWikiPageHistoryCountType.Minor,
            page.Latest.Id,
            page.Latest.Id,
            TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(MediaWikiErrorKeys.PageHistoryCountParametersInvalid, exception.ErrorKey);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageHtmlAsync_LivePage_ReturnsDocument()
    {
        var html = await wiki.Client.GetPageHtmlAsync(wiki.Page, TestContext.Current.CancellationToken);

        Assert.NotNull(html);
        Assert.Contains("<html", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageHtmlAsync_MissingPage_ReturnsNull()
    {
        var html = await wiki.Client.GetPageHtmlAsync(BuildMissingKey(), TestContext.Current.CancellationToken);

        Assert.Null(html);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageLanguageLinksAsync_LivePage_ReturnsWellFormedLinks()
    {
        var links = await wiki.Client.GetPageLanguageLinksAsync(wiki.Page, TestContext.Current.CancellationToken);

        Assert.NotNull(links);
        Assert.All(links, link =>
        {
            Assert.NotEmpty(link.Code);
            Assert.NotEmpty(link.Name);
            Assert.NotEmpty(link.Key);
            Assert.NotEmpty(link.Title);
        });
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageLanguageLinksAsync_MissingPage_ReturnsNull()
    {
        var links = await wiki.Client.GetPageLanguageLinksAsync(BuildMissingKey(), TestContext.Current.CancellationToken);

        Assert.Null(links);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageLintAsync_LivePage_ReturnsWellFormedErrors()
    {
        var errors = await SkipUnlessServedAsync(() => wiki.Client.GetPageLintAsync(wiki.Page, TestContext.Current.CancellationToken));

        Assert.NotNull(errors);
        Assert.All(errors, error =>
        {
            Assert.NotEmpty(error.Type);
            Assert.NotEmpty(error.Dsr);
        });
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageLintAsync_MissingPage_ReturnsNull()
    {
        var errors = await SkipUnlessServedAsync(() => wiki.Client.GetPageLintAsync(BuildMissingKey(), TestContext.Current.CancellationToken));

        Assert.Null(errors);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageWithHtmlAsync_LivePage_ReturnsMetadataAndHtml()
    {
        var page = await wiki.Client.GetPageWithHtmlAsync(wiki.Page, TestContext.Current.CancellationToken);

        Assert.NotNull(page);
        Assert.Equal(wiki.Page, page.Key);
        Assert.True(page.Latest.Id > 0);
        Assert.Contains("<html", page.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetPageWithHtmlAsync_MissingPage_ReturnsNull()
    {
        var page = await wiki.Client.GetPageWithHtmlAsync(BuildMissingKey(), TestContext.Current.CancellationToken);

        Assert.Null(page);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetRevisionAsync_LatestRevision_PointsBackAtPage()
    {
        var page = await GetPageBareAsync();

        var revision = await wiki.Client.GetRevisionAsync(page.Latest.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(revision);
        Assert.Equal(page.Latest.Id, revision.Id);
        Assert.Equal(page.Id, revision.Page.Id);
        Assert.Equal(page.Key, revision.Page.Key);
        Assert.Equal(page.ContentModel, revision.ContentModel);
        Assert.True(revision.Size > 0);
        Assert.NotEmpty(revision.Source);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetRevisionAsync_MissingRevision_ReturnsNull()
    {
        var revision = await wiki.Client.GetRevisionAsync(MissingRevisionId, TestContext.Current.CancellationToken);

        Assert.Null(revision);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetRevisionBareAsync_LatestRevision_PointsBackAtPage()
    {
        var page = await GetPageBareAsync();

        var revision = await wiki.Client.GetRevisionBareAsync(page.Latest.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(revision);
        Assert.Equal(page.Latest.Id, revision.Id);
        Assert.Equal(page.Id, revision.Page.Id);
        Assert.NotEmpty(revision.HtmlUrl);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetRevisionBareAsync_MissingRevision_ReturnsNull()
    {
        var revision = await wiki.Client.GetRevisionBareAsync(MissingRevisionId, TestContext.Current.CancellationToken);

        Assert.Null(revision);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetRevisionHtmlAsync_LatestRevision_ReturnsDocument()
    {
        var page = await GetPageBareAsync();

        var html = await wiki.Client.GetRevisionHtmlAsync(page.Latest.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(html);
        Assert.Contains("<html", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetRevisionHtmlAsync_MissingRevision_ReturnsNull()
    {
        var html = await wiki.Client.GetRevisionHtmlAsync(MissingRevisionId, TestContext.Current.CancellationToken);

        Assert.Null(html);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetRevisionLintAsync_LatestRevision_ReturnsWellFormedErrors()
    {
        var page = await GetPageBareAsync();

        var errors = await SkipUnlessServedAsync(() => wiki.Client.GetRevisionLintAsync(page.Latest.Id, TestContext.Current.CancellationToken));

        Assert.NotNull(errors);
        Assert.All(errors, error => Assert.NotEmpty(error.Type));
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetRevisionLintAsync_MissingRevision_ReturnsNull()
    {
        var errors = await SkipUnlessServedAsync(() => wiki.Client.GetRevisionLintAsync(MissingRevisionId, TestContext.Current.CancellationToken));

        Assert.Null(errors);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetRevisionWithHtmlAsync_LatestRevision_ReturnsMetadataAndHtml()
    {
        var page = await GetPageBareAsync();

        var revision = await wiki.Client.GetRevisionWithHtmlAsync(page.Latest.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(revision);
        Assert.Equal(page.Latest.Id, revision.Id);
        Assert.Contains("<html", revision.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task GetRevisionWithHtmlAsync_MissingRevision_ReturnsNull()
    {
        var revision = await wiki.Client.GetRevisionWithHtmlAsync(MissingRevisionId, TestContext.Current.CancellationToken);

        Assert.Null(revision);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task SearchPagesAsync_LivePageTitle_ReturnsWellFormedResults()
    {
        var results = await wiki.Client.SearchPagesAsync(wiki.Page.Replace('_', ' '), 5, TestContext.Current.CancellationToken);

        Assert.NotEmpty(results);
        Assert.InRange(results.Count, 1, 5);
        Assert.All(results, result =>
        {
            Assert.True(result.Id > 0);
            Assert.NotEmpty(result.Key);
            Assert.NotEmpty(result.Title);
        });
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task SearchPagesAsync_NoMatch_ReturnsEmpty()
    {
        var results = await wiki.Client.SearchPagesAsync(BuildMissingKey(), 5, TestContext.Current.CancellationToken);

        Assert.Empty(results);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task SearchTitlesAsync_LivePageTitle_ListsThePageUnderItsId()
    {
        var page = await GetPageBareAsync();

        var results = await wiki.Client.SearchTitlesAsync(wiki.Page.Replace('_', ' '), 5, TestContext.Current.CancellationToken);

        var result = Assert.Single(results, result => result.Key == page.Key);
        Assert.Equal(page.Id, result.Id);
        Assert.Equal(page.Title, result.Title);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task SearchTitlesAsync_LivePageTitle_ReturnsWellFormedResults()
    {
        var results = await wiki.Client.SearchTitlesAsync(wiki.Page.Replace('_', ' '), 5, TestContext.Current.CancellationToken);

        Assert.NotEmpty(results);
        Assert.InRange(results.Count, 1, 5);
        Assert.All(results, result =>
        {
            Assert.True(result.Id > 0);
            Assert.NotEmpty(result.Key);
            Assert.NotEmpty(result.Title);
        });
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task SearchTitlesAsync_NoMatch_ReturnsEmpty()
    {
        var results = await wiki.Client.SearchTitlesAsync(BuildMissingKey(), 5, TestContext.Current.CancellationToken);

        Assert.Empty(results);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task TransformHtmlToWikitextAsync_BoldElement_ReturnsBoldMarkup()
    {
        var wikitext = await wiki.Client.TransformHtmlToWikitextAsync("<b>bold</b>", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("'''bold'''", wikitext, StringComparison.Ordinal);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task TransformHtmlToWikitextAsync_WithPageContext_ReturnsBoldMarkup()
    {
        var page = await GetPageBareAsync();

        var wikitext = await wiki.Client.TransformHtmlToWikitextAsync("<b>bold</b>", page.Key, page.Latest.Id, TestContext.Current.CancellationToken);

        Assert.Contains("'''bold'''", wikitext, StringComparison.Ordinal);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task TransformWikitextToHtmlAsync_BoldMarkup_ReturnsBoldElement()
    {
        var html = await wiki.Client.TransformWikitextToHtmlAsync("'''bold'''", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("<b", html, StringComparison.Ordinal);
        Assert.Contains("bold</b>", html, StringComparison.Ordinal);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task TransformWikitextToHtmlAsync_WithPageContext_ReturnsBoldElement()
    {
        var page = await GetPageBareAsync();

        var html = await wiki.Client.TransformWikitextToHtmlAsync("'''bold'''", page.Key, page.Latest.Id, TestContext.Current.CancellationToken);

        Assert.Contains("bold</b>", html, StringComparison.Ordinal);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task TransformWikitextToLintAsync_UnclosedTag_ReportsMissingEndTag()
    {
        var errors = await SkipUnlessServedAsync(() =>
            wiki.Client.TransformWikitextToLintAsync("<div>unclosed", cancellationToken: TestContext.Current.CancellationToken));

        var error = Assert.Single(errors);
        Assert.Equal("missing-end-tag", error.Type);
        Assert.NotEmpty(error.Dsr);
        Assert.Null(error.TemplateInfo);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task TransformWikitextToLintAsync_WithPageContext_ReportsMissingEndTag()
    {
        var page = await GetPageBareAsync();

        var errors = await SkipUnlessServedAsync(() =>
            wiki.Client.TransformWikitextToLintAsync("<div>unclosed", page.Key, page.Latest.Id, TestContext.Current.CancellationToken));

        Assert.Equal("missing-end-tag", Assert.Single(errors).Type);
    }

    /// <summary>A page key no wiki has, with a fresh suffix so that nothing created since could collide with it.</summary>
    private static string BuildMissingKey()
    {
        return $"Smoke_test_{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Runs a call against an endpoint younger than the LTS releases, skipping the test rather than failing it when the
    /// wiki answers <c>rest-no-match</c>.
    /// </summary>
    private static async Task<T> SkipUnlessServedAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (MediaWikiException exception) when (exception.ErrorKey == MediaWikiErrorKeys.NoMatch)
        {
            Assert.Skip("The wiki's MediaWiki version predates this endpoint.");
            throw;
        }
    }

    /// <summary>
    /// Finds a page other than <paramref name="page"/> by searching for the first word of its title, skipping the test
    /// on a wiki too small for the search to find one.
    /// </summary>
    private async Task<MediaWikiPageBare> FindAnotherPageAsync(MediaWikiPageBare page)
    {
        var results = await wiki.Client.SearchPagesAsync(wiki.Page.Split('_')[0], 10, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var other = results.FirstOrDefault(result => result.Id != page.Id);

        Assert.SkipWhen(other is null, $"No page but \"{wiki.Page}\" turned up in search, so there is no second page to use.");

        var otherPage = await wiki.Client.GetPageBareAsync(other.Key, TestContext.Current.CancellationToken).ConfigureAwait(false);

        Assert.NotNull(otherPage);
        return otherPage;
    }

    private async Task<MediaWikiPageBare> GetPageBareAsync()
    {
        var page = await wiki.Client.GetPageBareAsync(wiki.Page, TestContext.Current.CancellationToken).ConfigureAwait(false);

        Assert.NotNull(page);
        return page;
    }
}
