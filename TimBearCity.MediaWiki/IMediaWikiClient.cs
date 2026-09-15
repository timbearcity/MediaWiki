using TimBearCity.MediaWiki.Files;
using TimBearCity.MediaWiki.Pages;
using TimBearCity.MediaWiki.Revisions;
using TimBearCity.MediaWiki.Search;
using TimBearCity.MediaWiki.Transform;

namespace TimBearCity.MediaWiki;

/// <summary>
/// A thin wrapper over the MediaWiki REST API (<c>/w/rest.php/v1/</c>).
/// </summary>
/// <remarks>
/// <para>
/// Every method that names one page, revision or file answers with <see langword="null"/> when the wiki does not
/// have it, and throws <see cref="MediaWikiException"/> for every other failure. The write methods are the
/// exception: a page that could not be written is a failure, not an absent result.
/// </para>
/// <para>
/// Absence is read from the error key in the <c>404</c> body rather than from the status alone: a <c>404</c> for an
/// endpoint the wiki's version does not serve (<c>rest-no-match</c>), or one with no MediaWiki error body at all,
/// throws.
/// </para>
/// </remarks>
public interface IMediaWikiClient
{
    /// <summary>
    /// Compares two revisions of the same page.
    /// </summary>
    /// <remarks>
    /// The comparison is line based, with up to two unchanged lines of context around each change, and reports
    /// moved paragraphs as a pair of entries linked to each other.
    /// </remarks>
    /// <param name="fromRevisionId">The revision used as the base of the comparison.</param>
    /// <param name="toRevisionId">The revision compared against it.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The comparison, or <see langword="null"/> if either revision does not exist.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fromRevisionId"/> or <paramref name="toRevisionId"/> is zero or negative.</exception>
    /// <exception cref="MediaWikiException">
    /// The request failed, timed out, or returned a response that could not be read. Revisions of different pages are refused
    /// with <see cref="System.Net.HttpStatusCode.BadRequest"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<MediaWikiRevisionComparison?> CompareRevisionsAsync(long fromRevisionId, long toRevisionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a page and returns it as the wiki stored it.
    /// </summary>
    /// <remarks>
    /// Writing requires an authenticated client: set <see cref="MediaWikiOptions.AccessToken"/> to an OAuth2 token
    /// or personal access token with the rights the wiki asks for.
    /// </remarks>
    /// <param name="title">The title of the page to create, e.g. <c>Wikipedia:Sandbox</c>.</param>
    /// <param name="source">The page content, in the format named by <paramref name="contentModel"/>. May be empty.</param>
    /// <param name="comment">The edit summary. The wiki insists on the field but accepts it empty.</param>
    /// <param name="contentModel">The content model the source is written in, or <see langword="null"/> for the wiki's default, which is normally <c>wikitext</c>.</param>
    /// <param name="csrfToken">A CSRF token, needed only when the request is authenticated with cookies rather than a bearer token.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The created page, including the revision the creation produced.</returns>
    /// <exception cref="ArgumentException"><paramref name="title"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="comment"/> is <see langword="null"/>.</exception>
    /// <exception cref="MediaWikiException">
    /// The request failed, timed out, or returned a response that could not be read. A page that already exists is
    /// reported as <see cref="System.Net.HttpStatusCode.Conflict"/> with the error key <c>apierror-articleexists</c>, and a
    /// wiki that refused the edit as <see cref="System.Net.HttpStatusCode.Forbidden"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<MediaWikiPage> CreatePageAsync(
        string title,
        string source,
        string comment,
        string? contentModel = null,
        string? csrfToken = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a file, along with the renditions the wiki serves it in.
    /// </summary>
    /// <remarks>
    /// A wiki that draws on a shared repository, as Wikipedia does on Wikimedia Commons, answers for the files it
    /// borrows as well as the ones it hosts.
    /// </remarks>
    /// <param name="title">The file's title, with or without its namespace, e.g. <c>File:Fennec Fox.jpg</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The file, or <see langword="null"/> if it does not exist.</returns>
    /// <exception cref="ArgumentException"><paramref name="title"/> is empty or whitespace.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<MediaWikiFile?> GetFileAsync(string title, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the standard thumbnail sizes a wiki renders a file at.
    /// </summary>
    /// <param name="title">The file's title, with or without its namespace, e.g. <c>File:Fennec Fox.jpg</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>
    /// The thumbnails, or <see langword="null"/> if the file does not exist or the wiki cannot render it, as with audio
    /// or a document type it has no handler for.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="title"/> is empty or whitespace.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<MediaWikiFileThumbnails?> GetFileThumbnailsAsync(string title, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single page, including its wikitext source.
    /// </summary>
    /// <param name="key">The page key or title, e.g. <c>Albert_Einstein</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The page, or <see langword="null"/> if it does not exist.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<MediaWikiPage?> GetPageAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single page's metadata, without its content, along with the URL its rendered HTML is served from.
    /// </summary>
    /// <param name="key">The page key or title, e.g. <c>Albert_Einstein</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The page, or <see langword="null"/> if it does not exist.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<MediaWikiPageBare?> GetPageBareAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the files used on a page.
    /// </summary>
    /// <remarks>
    /// The files are reported without their thumbnail rendition, which only <see cref="GetFileAsync"/> returns.
    /// </remarks>
    /// <param name="key">The page key or title, e.g. <c>Albert_Einstein</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The files, empty if the page uses none, or <see langword="null"/> if the page does not exist.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<IReadOnlyList<MediaWikiFile>?> GetPageFilesAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves one page of a page's revision history, newest first.
    /// </summary>
    /// <remarks>
    /// The wiki returns at most 20 revisions at a time. To walk further back, pass the identifier of the oldest
    /// revision you were given as <paramref name="olderThan"/>; to walk forward, pass the newest as
    /// <paramref name="newerThan"/>. The two cannot be combined.
    /// </remarks>
    /// <param name="key">The page key or title, e.g. <c>Albert_Einstein</c>.</param>
    /// <param name="olderThan">Return revisions older than this one, rather than the newest.</param>
    /// <param name="newerThan">Return revisions newer than this one, rather than the newest.</param>
    /// <param name="filter">Return only revisions of one kind, rather than all of them.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The history, or <see langword="null"/> if the page does not exist.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="olderThan"/> or <paramref name="newerThan"/> is zero or negative, or <paramref name="filter"/> is
    /// not one of the defined values.
    /// </exception>
    /// <exception cref="MediaWikiException">
    /// The request failed, timed out, or returned a response that could not be read. Combining <paramref name="olderThan"/> with
    /// <paramref name="newerThan"/> is refused with <see cref="System.Net.HttpStatusCode.BadRequest"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<MediaWikiPageHistory?> GetPageHistoryAsync(
        string key,
        long? olderThan = null,
        long? newerThan = null,
        MediaWikiPageHistoryFilter? filter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts a page's revisions of one kind.
    /// </summary>
    /// <remarks>
    /// The wiki stops counting at a limit that differs by type, and says so through
    /// <see cref="MediaWikiPageHistoryCount.IsLimitExceeded"/> rather than by failing.
    /// </remarks>
    /// <param name="key">The page key or title, e.g. <c>Albert_Einstein</c>.</param>
    /// <param name="type">The kind of revision to count.</param>
    /// <param name="fromRevisionId">Count from this revision, rather than from the beginning of the history.</param>
    /// <param name="toRevisionId">Count up to this revision, rather than to the end of the history.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The count, or <see langword="null"/> if the page does not exist.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="fromRevisionId"/> or <paramref name="toRevisionId"/> is zero or negative, or
    /// <paramref name="type"/> is not one of the defined values.
    /// </exception>
    /// <exception cref="MediaWikiException">
    /// The request failed, timed out, or returned a response that could not be read. A range is only accepted for
    /// <see cref="MediaWikiPageHistoryCountType.Edits"/> and <see cref="MediaWikiPageHistoryCountType.Editors"/>, and only
    /// with both ends given; anything else is refused with <see cref="System.Net.HttpStatusCode.BadRequest"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<MediaWikiPageHistoryCount?> GetPageHistoryCountAsync(
        string key,
        MediaWikiPageHistoryCountType type,
        long? fromRevisionId = null,
        long? toRevisionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single page's rendered HTML, without the metadata that accompanies it elsewhere.
    /// </summary>
    /// <remarks>
    /// The markup is returned as it arrives, for the caller to hand to the HTML parser of their choice.
    /// A redirect page is answered with the target's HTML.
    /// </remarks>
    /// <param name="key">The page key or title, e.g. <c>Albert_Einstein</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The page's HTML, or <see langword="null"/> if the page does not exist.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<string?> GetPageHtmlAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the same page on the other wikis in this one's family.
    /// </summary>
    /// <param name="key">The page key or title, e.g. <c>Albert_Einstein</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The interlanguage links, empty if the page has none, or <see langword="null"/> if the page does not exist.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<IReadOnlyList<MediaWikiPageLanguageLink>?> GetPageLanguageLinksAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the problems the wiki's linter found in a page's markup.
    /// </summary>
    /// <param name="key">The page key or title, e.g. <c>Albert_Einstein</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The lint errors, empty if the markup is clean, or <see langword="null"/> if the page does not exist.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<IReadOnlyList<MediaWikiLintError>?> GetPageLintAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single page, including its rendered HTML.
    /// </summary>
    /// <remarks>
    /// A redirect page is answered with the target's HTML, so the returned
    /// <see cref="MediaWikiPageMetadata.Key"/> may differ from <paramref name="key"/>.
    /// </remarks>
    /// <param name="key">The page key or title, e.g. <c>Albert_Einstein</c>.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The page, or <see langword="null"/> if it does not exist.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<MediaWikiPageWithHtml?> GetPageWithHtmlAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single revision, including its source.
    /// </summary>
    /// <param name="id">The revision identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The revision, or <see langword="null"/> if it does not exist.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero or negative.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<MediaWikiRevision?> GetRevisionAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single revision's metadata, without its content, along with the URL its rendered HTML is served from.
    /// </summary>
    /// <param name="id">The revision identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The revision, or <see langword="null"/> if it does not exist.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero or negative.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<MediaWikiRevisionBare?> GetRevisionBareAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single revision's rendered HTML, without the metadata that accompanies it elsewhere.
    /// </summary>
    /// <remarks>The markup is returned as it arrives, for the caller to hand to the HTML parser of their choice.</remarks>
    /// <param name="id">The revision identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The revision's HTML, or <see langword="null"/> if the revision does not exist.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero or negative.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<string?> GetRevisionHtmlAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the problems the wiki's linter found in a revision's markup.
    /// </summary>
    /// <param name="id">The revision identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The lint errors, empty if the markup is clean, or <see langword="null"/> if the revision does not exist.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero or negative.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<IReadOnlyList<MediaWikiLintError>?> GetRevisionLintAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single revision, including its rendered HTML.
    /// </summary>
    /// <param name="id">The revision identifier.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The revision, or <see langword="null"/> if it does not exist.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="id"/> is zero or negative.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<MediaWikiRevisionWithHtml?> GetRevisionWithHtmlAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches wiki pages for <paramref name="query"/>.
    /// </summary>
    /// <param name="query">The search term. Must not be empty.</param>
    /// <param name="limit">Maximum number of results, between 1 and 100.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The matching page summaries in relevance order, at most <paramref name="limit"/> of them; empty if nothing matched.</returns>
    /// <exception cref="ArgumentException"><paramref name="query"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside 1 to 100.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<IReadOnlyList<MediaWikiSearchResult>> SearchPagesAsync(string query, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches wiki page titles for <paramref name="query"/>, for completing what someone is typing.
    /// </summary>
    /// <remarks>
    /// This matches titles rather than content, so it answers faster and more narrowly than
    /// <see cref="SearchPagesAsync"/>, and each result carries the title in place of an excerpt.
    /// </remarks>
    /// <param name="query">The search term. Must not be empty.</param>
    /// <param name="limit">Maximum number of results, between 1 and 100.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The matching page summaries in relevance order, at most <paramref name="limit"/> of them; empty if nothing matched.</returns>
    /// <exception cref="ArgumentException"><paramref name="query"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside 1 to 100.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<IReadOnlyList<MediaWikiSearchResult>> SearchTitlesAsync(string query, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts HTML to wikitext, without saving anything.
    /// </summary>
    /// <remarks>
    /// Round-tripping is only lossless for HTML this wiki produced: name the <paramref name="title"/> and
    /// <paramref name="revisionId"/> the HTML came from, and the wiki reuses the original wikitext for the parts
    /// that did not change.
    /// </remarks>
    /// <param name="html">The HTML to convert. May be empty.</param>
    /// <param name="title">The page the HTML belongs to, which decides how its links and templates resolve.</param>
    /// <param name="revisionId">The revision the HTML was rendered from. Requires <paramref name="title"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The converted wikitext.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="html"/> is <see langword="null"/>, or <paramref name="title"/> is empty, whitespace, or absent while
    /// <paramref name="revisionId"/> is given.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="html"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="revisionId"/> is zero or negative.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<string> TransformHtmlToWikitextAsync(
        string html,
        string? title = null,
        long? revisionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders wikitext as HTML, without saving anything.
    /// </summary>
    /// <remarks>
    /// Naming a <paramref name="title"/> gives the wikitext a page to be relative to, which is what decides how its
    /// links, templates and magic words resolve.
    /// </remarks>
    /// <param name="wikitext">The wikitext to render. May be empty.</param>
    /// <param name="title">The page the wikitext belongs to, which decides how its links and templates resolve.</param>
    /// <param name="revisionId">The revision the wikitext is based on. Requires <paramref name="title"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The rendered HTML.</returns>
    /// <exception cref="ArgumentException"><paramref name="title"/> is empty, whitespace, or absent while <paramref name="revisionId"/> is given.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="wikitext"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="revisionId"/> is zero or negative.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<string> TransformWikitextToHtmlAsync(
        string wikitext,
        string? title = null,
        long? revisionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lints wikitext, without saving anything.
    /// </summary>
    /// <remarks>Use this to check an edit before making it, rather than reading back the problems afterward.</remarks>
    /// <param name="wikitext">The wikitext to lint. May be empty.</param>
    /// <param name="title">The page the wikitext belongs to, which decides how its links and templates resolve.</param>
    /// <param name="revisionId">The revision the wikitext is based on. Requires <paramref name="title"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The lint errors, empty if the markup is clean.</returns>
    /// <exception cref="ArgumentException"><paramref name="title"/> is empty, whitespace, or absent while <paramref name="revisionId"/> is given.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="wikitext"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="revisionId"/> is zero or negative.</exception>
    /// <exception cref="MediaWikiException">The request failed, timed out, or returned a response that could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<IReadOnlyList<MediaWikiLintError>> TransformWikitextToLintAsync(
        string wikitext,
        string? title = null,
        long? revisionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a page and returns it as the wiki stored it, creating it if it does not exist.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pass the <see cref="MediaWikiRevisionReference.Id"/> the page was read at as <paramref name="latestRevisionId"/>: the
    /// wiki rejects the edit rather than overwrite someone else's if the page moved on in the meantime.
    /// </para>
    /// <para>
    /// Leaving <paramref name="latestRevisionId"/> unset asks for a creation instead, so an existing page is
    /// refused with <see cref="System.Net.HttpStatusCode.Conflict"/>.
    /// </para>
    /// <para>
    /// Writing requires an authenticated client: set <see cref="MediaWikiOptions.AccessToken"/> to an OAuth2 token
    /// or personal access token with the rights the wiki asks for.
    /// </para>
    /// </remarks>
    /// <param name="key">The page key or title, e.g. <c>Wikipedia:Sandbox</c>.</param>
    /// <param name="source">The new page content, in the format named by <paramref name="contentModel"/>. May be empty, which blanks the page.</param>
    /// <param name="comment">The edit summary. The wiki insists on the field but accepts it empty.</param>
    /// <param name="latestRevisionId">The identifier of the revision this edit was based on, or <see langword="null"/> to create the page.</param>
    /// <param name="contentModel">The content model the source is written in, or <see langword="null"/> to leave the page's own unchanged.</param>
    /// <param name="csrfToken">A CSRF token, needed only when the request is authenticated with cookies rather than a bearer token.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The stored page, including the revision the edit produced.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="comment"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="latestRevisionId"/> is zero or negative.</exception>
    /// <exception cref="MediaWikiException">
    /// The request failed, timed out, or returned a response that could not be read. An edit conflict is reported as
    /// <see cref="System.Net.HttpStatusCode.Conflict"/> with the error key <c>editconflict</c> (<c>edit-conflict</c>
    /// on MediaWiki 1.43), an existing page met without a <paramref name="latestRevisionId"/> as
    /// <c>rest-update-cannot-create-page</c>, a page that has since been deleted as
    /// <see cref="System.Net.HttpStatusCode.NotFound"/>, and a refused edit as
    /// <see cref="System.Net.HttpStatusCode.Forbidden"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    Task<MediaWikiPage> UpdatePageAsync(
        string key,
        string source,
        string comment,
        long? latestRevisionId = null,
        string? contentModel = null,
        string? csrfToken = null,
        CancellationToken cancellationToken = default);
}
