using System.Net;
using TimBearCity.MediaWiki.Pages;
using TimBearCity.MediaWiki.Revisions;
using Xunit;

namespace TimBearCity.MediaWiki.Tests;

/// <summary>
/// Edits against a throwaway wiki, proving the write paths and the shapes they return against a real MediaWiki rather
/// than the canned responses the rest of the suite uses.
/// </summary>
/// <remarks>
/// <para>
/// Opt in by setting <c>MEDIAWIKI_SMOKE_WRITE_BASE_URL</c> to the REST API root of a wiki that exists only for the
/// run, such as the Docker container the CI job starts; without it every test here is skipped. These tests create
/// pages and never clean them up, so they must not be pointed at a wiki anyone cares about, and they deliberately do
/// not read <c>MEDIAWIKI_SMOKE_READ_BASE_URL</c>.
/// </para>
/// <para>
/// Every test creates its own page under a fresh title, so the tests neither depend on each other nor on earlier runs.
/// </para>
/// </remarks>
public sealed class MediaWikiClientWriteSmokeTests(WritableWikiFixture wiki) : IClassFixture<WritableWikiFixture>
{
    private const string NotConfigured = "Set MEDIAWIKI_SMOKE_WRITE_BASE_URL to the REST API root of a throwaway wiki to run the write smoke tests.";

    public static bool IsEnabled => WritableWikiFixture.BaseUrl is not null;

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task CreatePageAsync_EmptyComment_LetsTheWikiSummarize()
    {
        var title = WritableWikiFixture.BuildNewTitle();

        var created = await wiki.Client.CreatePageAsync(title, "Created without a summary.", string.Empty, csrfToken: wiki.CsrfToken,
            cancellationToken: TestContext.Current.CancellationToken);

        var revision = await wiki.Client.GetRevisionBareAsync(created.Latest.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(revision);
        Assert.False(string.IsNullOrEmpty(revision.Comment));
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task CreatePageAsync_EmptySource_CreatesEmptyPage()
    {
        var title = WritableWikiFixture.BuildNewTitle();

        var created = await CreateAsync(title, string.Empty);

        Assert.Equal(string.Empty, created.Source);

        var read = await wiki.Client.GetPageAsync(title, TestContext.Current.CancellationToken);

        Assert.NotNull(read);
        Assert.Equal(string.Empty, read.Source);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task CreatePageAsync_ExistingTitle_ThrowsConflict()
    {
        var title = WritableWikiFixture.BuildNewTitle();
        await CreateAsync(title, "First.");

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => CreateAsync(title, "Second."));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal(MediaWikiErrorKeys.ArticleExists, exception.ErrorKey);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task CreatePageAsync_ExplicitContentModel_ReturnsThatModel()
    {
        var title = WritableWikiFixture.BuildNewTitle();

        var created = await wiki.Client.CreatePageAsync(
            title,
            "Created with the content model named.",
            "Created by the write smoke tests.",
            "wikitext",
            wiki.CsrfToken,
            TestContext.Current.CancellationToken);

        Assert.Equal("wikitext", created.ContentModel);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task CreatePageAsync_MalformedTitle_ThrowsBadRequest()
    {
        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => CreateAsync("<>", "Never stored."));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(MediaWikiErrorKeys.InvalidTitleOnEdit, exception.ErrorKey);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task CreatePageAsync_NewTitle_ReturnsPageAsStored()
    {
        var title = WritableWikiFixture.BuildNewTitle();

        var created = await CreateAsync(title, "Created by the write smoke tests.");

        Assert.True(created.Id > 0);
        Assert.Equal(title, created.Key);
        Assert.Equal(title, created.Title);
        Assert.Equal("wikitext", created.ContentModel);
        Assert.Equal("Created by the write smoke tests.", created.Source);
        Assert.True(created.Latest.Id > 0);

        var read = await wiki.Client.GetPageAsync(title, TestContext.Current.CancellationToken);

        Assert.NotNull(read);
        Assert.Equal(created, read);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task UpdatePageAsync_EmptySource_BlanksPage()
    {
        var title = WritableWikiFixture.BuildNewTitle();
        var created = await CreateAsync(title, "About to be blanked.");

        var updated = await wiki.Client.UpdatePageAsync(title, string.Empty, "Blanked by the write smoke tests.", created.Latest.Id,
            csrfToken: wiki.CsrfToken, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, updated.Source);
        Assert.True(updated.Latest.Id > created.Latest.Id);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task UpdatePageAsync_ExistingPageWithoutRevision_ThrowsConflict()
    {
        var title = WritableWikiFixture.BuildNewTitle();
        await CreateAsync(title, "Exists.");

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => wiki.Client.UpdatePageAsync(
            title,
            "Meant as a creation.",
            "Created by the write smoke tests.",
            csrfToken: wiki.CsrfToken,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal(MediaWikiErrorKeys.UpdateCannotCreatePage, exception.ErrorKey);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task UpdatePageAsync_ExplicitContentModel_KeepsThatModel()
    {
        var title = WritableWikiFixture.BuildNewTitle();
        var created = await CreateAsync(title, "Before the update.");

        var updated = await wiki.Client.UpdatePageAsync(
            title,
            "After the update.",
            "Updated by the write smoke tests.",
            created.Latest.Id,
            "wikitext",
            wiki.CsrfToken,
            TestContext.Current.CancellationToken);

        Assert.Equal("wikitext", updated.ContentModel);
        Assert.Equal("After the update.", updated.Source);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task UpdatePageAsync_LatestRevision_StoresNewRevision()
    {
        var title = WritableWikiFixture.BuildNewTitle();
        var created = await CreateAsync(title, "Before the update.");

        var updated = await wiki.Client.UpdatePageAsync(
            title,
            "After the update.",
            "Updated by the write smoke tests.",
            created.Latest.Id,
            csrfToken: wiki.CsrfToken,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("After the update.", updated.Source);
        Assert.True(updated.Latest.Id > created.Latest.Id);

        var revision = await wiki.Client.GetRevisionAsync(updated.Latest.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(revision);
        Assert.Equal(created.Id, revision.Page.Id);
        Assert.Equal("Updated by the write smoke tests.", revision.Comment);
        Assert.Equal("After the update.", revision.Source);

        var history = await wiki.Client.GetPageHistoryAsync(title, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(history);
        Assert.Equal([updated.Latest.Id, created.Latest.Id], history.Revisions.Select(entry => entry.Id));

        var comparison = await wiki.Client.CompareRevisionsAsync(created.Latest.Id, updated.Latest.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(comparison);
        Assert.Contains(comparison.Diff, entry => entry.Type == MediaWikiDiffType.Changed);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task UpdatePageAsync_MissingPageWithRevision_ThrowsNotFound()
    {
        var elsewhere = await CreateAsync(WritableWikiFixture.BuildNewTitle(), "Lends its revision.");

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => wiki.Client.UpdatePageAsync(
            WritableWikiFixture.BuildNewTitle(),
            "Never stored.",
            "Updated by the write smoke tests.",
            elsewhere.Latest.Id,
            csrfToken: wiki.CsrfToken,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal(MediaWikiErrorKeys.MissingTitle, exception.ErrorKey);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task UpdatePageAsync_MissingPageWithoutRevision_CreatesPage()
    {
        var title = WritableWikiFixture.BuildNewTitle();

        var created = await wiki.Client.UpdatePageAsync(title, "Created through an update.", "Created by the write smoke tests.",
            csrfToken: wiki.CsrfToken, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(created.Id > 0);
        Assert.Equal(title, created.Key);
        Assert.Equal("Created through an update.", created.Source);

        var history = await wiki.Client.GetPageHistoryAsync(title, cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(history);
        var revision = Assert.Single(history.Revisions);
        Assert.Equal(created.Latest.Id, revision.Id);
    }

    [Fact(Skip = NotConfigured, SkipUnless = nameof(IsEnabled))]
    public async Task UpdatePageAsync_StaleRevision_ThrowsEditConflict()
    {
        var title = WritableWikiFixture.BuildNewTitle();
        var created = await CreateAsync(title, "Original.");
        await wiki.Client.UpdatePageAsync(title, "Someone else's edit.", "Updated by the write smoke tests.", created.Latest.Id,
            csrfToken: wiki.CsrfToken, cancellationToken: TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => wiki.Client.UpdatePageAsync(
            title,
            "An edit based on the original.",
            "Updated by the write smoke tests.",
            created.Latest.Id,
            csrfToken: wiki.CsrfToken,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Contains(exception.ErrorKey, new[] { MediaWikiErrorKeys.EditConflict, MediaWikiErrorKeys.EditConflictHyphenated });
    }

    private async Task<MediaWikiPage> CreateAsync(string title, string source)
    {
        return await wiki.Client.CreatePageAsync(
            title,
            source,
            "Created by the write smoke tests.",
            csrfToken: wiki.CsrfToken,
            cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(false);
    }
}
