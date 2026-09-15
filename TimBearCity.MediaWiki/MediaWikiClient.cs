using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using TimBearCity.MediaWiki.Files;
using TimBearCity.MediaWiki.Pages;
using TimBearCity.MediaWiki.Revisions;
using TimBearCity.MediaWiki.Search;
using TimBearCity.MediaWiki.Serialization;
using TimBearCity.MediaWiki.Transform;

namespace TimBearCity.MediaWiki;

/// <inheritdoc cref="IMediaWikiClient"/>
/// <remarks>
/// Registered as a typed <see cref="HttpClient"/> by <see cref="ServiceCollectionExtensions"/>,
/// which supplies the base address, timeout and headers from <see cref="MediaWikiOptions"/>.
/// </remarks>
public sealed class MediaWikiClient : IMediaWikiClient
{
    /// <summary>
    /// The name of the <see cref="ActivitySource"/> under which the client traces each operation, e.g. <c>GetPage</c>,
    /// as a parent of the <see cref="HttpClient"/> request. Subscribe to it (<c>AddSource</c> in OpenTelemetry) to
    /// see the wiki (<c>server.address</c>) and, on failure, the status code, MediaWiki's error key and the exception
    /// type. Nothing is recorded without a subscriber.
    /// </summary>
    public const string ActivitySourceName = "TimBearCity.MediaWiki";

    /// <summary>The largest page count the MediaWiki search endpoints accept.</summary>
    public const int MaxSearchLimit = 100;

    /// <summary>The MediaWiki error key on a span, whether it failed the operation or only explained an absent result.</summary>
    private const string ErrorKeyTag = "mediawiki.error_key";

    // The OpenTelemetry semantic-convention tags: the first two on a failed span, the last on every span.
    private const string ErrorTypeTag = "error.type";
    private const string HttpResponseStatusCodeTag = "http.response.status_code";
    private const string ServerAddressTag = "server.address";

    /// <summary>
    /// The error keys under which MediaWiki answers <c>404</c> because the page, revision or file is not there. A
    /// title no page could have, such as one with angle brackets, counts too: the page endpoints answer it with
    /// <c>rest-invalid-title</c> where the rest answer <c>rest-nonexistent-title</c>. Any other <c>404</c> is
    /// something else: an endpoint the wiki's version does not serve (<c>rest-no-match</c>), a base URL that misses
    /// the REST API, or a web server answering for it.
    /// </summary>
    private static readonly FrozenSet<string> AbsentResourceErrorKeys = FrozenSet.ToFrozenSet(
    [
        "rest-cannot-load-file",
        "rest-compare-nonexistent",
        "rest-invalid-title",
        "rest-no-revision",
        "rest-nonexistent-revision",
        "rest-nonexistent-title",
        "rest-nonexistent-title-revision"
    ], StringComparer.Ordinal);

    /// <summary>See <see cref="ActivitySourceName"/>.</summary>
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>
    /// Asked for by the lint transform; see <see cref="SendAsync"/>. The endpoint answers JSON, but MediaWiki 1.43
    /// refuses a request that asks for JSON by name with <c>406</c>, and only a wildcard satisfies every version.
    /// </summary>
    private static readonly MediaTypeWithQualityHeaderValue AnyAccept = new("*/*");

    /// <summary>Asked for by the endpoints that answer with Parsoid HTML; see <see cref="SendAsync"/>.</summary>
    private static readonly MediaTypeWithQualityHeaderValue HtmlAccept = new(MediaTypeNames.Text.Html);

    /// <summary>The media type the write endpoints accept; sent bare, since the body is UTF-8 either way.</summary>
    private static readonly MediaTypeHeaderValue JsonMediaType = new(MediaTypeNames.Application.Json);

    /// <summary>Asked for by the endpoint that answers with wikitext; see <see cref="SendAsync"/>.</summary>
    private static readonly MediaTypeWithQualityHeaderValue PlainTextAccept = new(MediaTypeNames.Text.Plain);

    private readonly HttpClient _httpClient;

    /// <summary>Creates a client over an <see cref="HttpClient"/> that already has its base address configured.</summary>
    /// <param name="httpClient">The configured client; its <see cref="HttpClient.BaseAddress"/> must be set.</param>
    public MediaWikiClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        if (httpClient.BaseAddress is null)
        {
            throw new ArgumentException($"{nameof(HttpClient)}.{nameof(HttpClient.BaseAddress)} must be set. " +
                                        $"Register the client with {nameof(ServiceCollectionExtensions.AddMediaWikiClient)}.", nameof(httpClient));
        }

        _httpClient = httpClient;
    }

    /// <summary>
    /// The activity of the operation in progress, or <see langword="null"/> when nobody subscribed to
    /// <see cref="ActivitySource"/>: then <see cref="Activity.Current"/> is the caller's own span, which is not ours to tag.
    /// </summary>
    private static Activity? CurrentActivity =>
        Activity.Current is { } activity && ReferenceEquals(activity.Source, ActivitySource) ? activity : null;

    /// <inheritdoc/>
    public Task<MediaWikiRevisionComparison?> CompareRevisionsAsync(long fromRevisionId, long toRevisionId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fromRevisionId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(toRevisionId);

        var requestUri = string.Create(CultureInfo.InvariantCulture, $"revision/{fromRevisionId}/compare/{toRevisionId}");

        return GetJsonOrNullAsync(requestUri, MediaWikiJsonSerializerContext.Default.MediaWikiRevisionComparison, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<MediaWikiPage> CreatePageAsync(
        string title,
        string source,
        string comment,
        string? contentModel = null,
        string? csrfToken = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(comment);

        var request = new MediaWikiPageCreateRequest(title, source, comment, contentModel, csrfToken);

        // The title travels in the body here, so the endpoint is the collection rather than a page within it.
        return EditPageAsync("page", HttpMethod.Post, request, MediaWikiJsonSerializerContext.Default.MediaWikiPageCreateRequest, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<MediaWikiFile?> GetFileAsync(string title, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var requestUri = $"file/{Uri.EscapeDataString(title)}";

        return GetJsonOrNullAsync(requestUri, MediaWikiJsonSerializerContext.Default.MediaWikiFile, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<MediaWikiFileThumbnails?> GetFileThumbnailsAsync(string title, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var requestUri = $"file/{Uri.EscapeDataString(title)}/thumbnails";

        return GetJsonOrNullAsync(requestUri, MediaWikiJsonSerializerContext.Default.MediaWikiFileThumbnails, cancellationToken,
            "rest-file-not-thumbnailable");
    }

    /// <inheritdoc/>
    public Task<MediaWikiPage?> GetPageAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return GetJsonOrNullAsync(BuildPageUri(key), MediaWikiJsonSerializerContext.Default.MediaWikiPage, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<MediaWikiPageBare?> GetPageBareAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return GetJsonOrNullAsync(BuildPageUri(key, "bare"), MediaWikiJsonSerializerContext.Default.MediaWikiPageBare, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MediaWikiFile>?> GetPageFilesAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var filesResponse = await GetJsonOrNullAsync(
            BuildPageUri(key, "links/media"),
            MediaWikiJsonSerializerContext.Default.MediaWikiPageFilesResponse,
            cancellationToken).ConfigureAwait(false);

        return filesResponse?.Files;
    }

    /// <inheritdoc/>
    public Task<MediaWikiPageHistory?> GetPageHistoryAsync(
        string key,
        long? olderThan = null,
        long? newerThan = null,
        MediaWikiPageHistoryFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        List<string> query = [];

        if (olderThan is { } olderRevisionId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(olderRevisionId, nameof(olderThan));

            query.Add(string.Create(CultureInfo.InvariantCulture, $"older_than={olderRevisionId}"));
        }

        if (newerThan is { } newerRevisionId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(newerRevisionId, nameof(newerThan));

            query.Add(string.Create(CultureInfo.InvariantCulture, $"newer_than={newerRevisionId}"));
        }

        if (filter is { } historyFilter)
        {
            query.Add($"filter={GetQueryValue(historyFilter)}");
        }

        var requestUri = AppendQuery(BuildPageUri(key, "history"), query);

        return GetJsonOrNullAsync(requestUri, MediaWikiJsonSerializerContext.Default.MediaWikiPageHistory, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<MediaWikiPageHistoryCount?> GetPageHistoryCountAsync(
        string key,
        MediaWikiPageHistoryCountType type,
        long? fromRevisionId = null,
        long? toRevisionId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        List<string> query = [];

        if (fromRevisionId is { } from)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(from, nameof(fromRevisionId));

            query.Add(string.Create(CultureInfo.InvariantCulture, $"from={from}"));
        }

        if (toRevisionId is { } to)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(to, nameof(toRevisionId));

            query.Add(string.Create(CultureInfo.InvariantCulture, $"to={to}"));
        }

        var requestUri = AppendQuery($"{BuildPageUri(key, "history")}/counts/{GetQueryValue(type)}", query);

        return GetJsonOrNullAsync(requestUri, MediaWikiJsonSerializerContext.Default.MediaWikiPageHistoryCount, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string?> GetPageHtmlAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return GetTextOrNullAsync(BuildPageUri(key, "html"), HtmlAccept, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<MediaWikiPageLanguageLink>?> GetPageLanguageLinksAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return GetJsonOrNullAsync(BuildPageUri(key, "links/language"), MediaWikiJsonSerializerContext.Default.PageLanguageLinks, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<MediaWikiLintError>?> GetPageLintAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return GetJsonOrNullAsync(BuildPageUri(key, "lint"), MediaWikiJsonSerializerContext.Default.LintErrors, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<MediaWikiPageWithHtml?> GetPageWithHtmlAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return GetJsonOrNullAsync(BuildPageUri(key, "with_html"), MediaWikiJsonSerializerContext.Default.MediaWikiPageWithHtml, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<MediaWikiRevision?> GetRevisionAsync(long id, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return GetJsonOrNullAsync(BuildRevisionUri(id), MediaWikiJsonSerializerContext.Default.MediaWikiRevision, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<MediaWikiRevisionBare?> GetRevisionBareAsync(long id, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return GetJsonOrNullAsync(BuildRevisionUri(id, "bare"), MediaWikiJsonSerializerContext.Default.MediaWikiRevisionBare, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string?> GetRevisionHtmlAsync(long id, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return GetTextOrNullAsync(BuildRevisionUri(id, "html"), HtmlAccept, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<MediaWikiLintError>?> GetRevisionLintAsync(long id, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return GetJsonOrNullAsync(BuildRevisionUri(id, "lint"), MediaWikiJsonSerializerContext.Default.LintErrors, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<MediaWikiRevisionWithHtml?> GetRevisionWithHtmlAsync(long id, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return GetJsonOrNullAsync(BuildRevisionUri(id, "with_html"), MediaWikiJsonSerializerContext.Default.MediaWikiRevisionWithHtml, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<MediaWikiSearchResult>> SearchPagesAsync(string query, int limit = 50, CancellationToken cancellationToken = default)
    {
        return SearchAsync("search/page", query, limit, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<MediaWikiSearchResult>> SearchTitlesAsync(string query, int limit = 50, CancellationToken cancellationToken = default)
    {
        return SearchAsync("search/title", query, limit, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string> TransformHtmlToWikitextAsync(
        string html,
        string? title = null,
        long? revisionId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(html);

        var requestUri = BuildTransformUri("html/to/wikitext", title, revisionId);
        var request = new MediaWikiTransformHtmlRequest(html);

        return TransformAsync(requestUri, request, MediaWikiJsonSerializerContext.Default.MediaWikiTransformHtmlRequest, PlainTextAccept, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string> TransformWikitextToHtmlAsync(
        string wikitext,
        string? title = null,
        long? revisionId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wikitext);

        var requestUri = BuildTransformUri("wikitext/to/html", title, revisionId);
        var request = new MediaWikiTransformWikitextRequest(wikitext);

        return TransformAsync(requestUri, request, MediaWikiJsonSerializerContext.Default.MediaWikiTransformWikitextRequest, HtmlAccept, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MediaWikiLintError>> TransformWikitextToLintAsync(
        string wikitext,
        string? title = null,
        long? revisionId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wikitext);

        var requestUri = BuildTransformUri("wikitext/to/lint", title, revisionId);
        var request = new MediaWikiTransformWikitextRequest(wikitext);

        using var activity = StartActivity(nameof(TransformWikitextToLintAsync));
        using var response = await SendJsonAsync(
            HttpMethod.Post,
            requestUri,
            request,
            MediaWikiJsonSerializerContext.Default.MediaWikiTransformWikitextRequest,
            AnyAccept,
            cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(requestUri, response, cancellationToken).ConfigureAwait(false);

        return await ReadJsonAsync(requestUri, response, MediaWikiJsonSerializerContext.Default.LintErrors, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<MediaWikiPage> UpdatePageAsync(
        string key,
        string source,
        string comment,
        long? latestRevisionId = null,
        string? contentModel = null,
        string? csrfToken = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(comment);

        MediaWikiPageUpdateRequest.BaseRevision? latest = null;

        if (latestRevisionId is { } revisionId)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revisionId, nameof(latestRevisionId));

            latest = new MediaWikiPageUpdateRequest.BaseRevision(revisionId);
        }

        var request = new MediaWikiPageUpdateRequest(source, comment, latest, contentModel, csrfToken);

        return EditPageAsync(BuildPageUri(key), HttpMethod.Put, request, MediaWikiJsonSerializerContext.Default.MediaWikiPageUpdateRequest, cancellationToken);
    }

    /// <summary>Appends the query string, when there is one to append.</summary>
    private static string AppendQuery(string requestUri, List<string> query)
    {
        return query.Count == 0 ? requestUri : $"{requestUri}?{string.Join('&', query)}";
    }

    /// <summary>The endpoint for one representation of a page.</summary>
    /// <param name="key">The page key, escaped into the request path.</param>
    /// <param name="representation">The path segment selecting the representation, or <see langword="null"/> for the source.</param>
    private static string BuildPageUri(string key, string? representation = null)
    {
        return $"page/{Uri.EscapeDataString(key)}{(representation is null ? null : $"/{representation}")}";
    }

    /// <summary>The endpoint for one representation of a revision.</summary>
    /// <param name="id">The revision identifier.</param>
    /// <param name="representation">The path segment selecting the representation, or <see langword="null"/> for the source.</param>
    private static string BuildRevisionUri(long id, string? representation = null)
    {
        return string.Create(CultureInfo.InvariantCulture, $"revision/{id}{(representation is null ? null : $"/{representation}")}");
    }

    /// <summary>The endpoint for one conversion, in the context of a page and revision where the caller named them.</summary>
    /// <param name="conversion">The path segments naming the conversion, e.g. <c>wikitext/to/html</c>.</param>
    /// <param name="title">The page the content belongs to, which decides how its links and templates resolve.</param>
    /// <param name="revisionId">The revision the content is based on.</param>
    private static string BuildTransformUri(string conversion, string? title, long? revisionId)
    {
        if (title is null)
        {
            if (revisionId is not null)
            {
                throw new ArgumentException(
                    $"A revision only means something alongside the page it belongs to, so {nameof(title)} is required when {nameof(revisionId)} is given.",
                    nameof(title));
            }

            return $"transform/{conversion}";
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var requestUri = $"transform/{conversion}/{Uri.EscapeDataString(title)}";

        if (revisionId is not { } revision)
        {
            return requestUri;
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision, nameof(revisionId));

        return string.Create(CultureInfo.InvariantCulture, $"{requestUri}/{revision}");
    }

    /// <summary>Builds the <see cref="MediaWikiException"/> describing a failed response.</summary>
    private static MediaWikiException CreateException(string requestUri, HttpResponseMessage response, MediaWikiError? error)
    {
        var reason = response.ReasonPhrase ?? error?.HttpReason;
        var detail = error?.Describe();
        var hint = GetNotFoundHint(response, error);

        var message = string.Create(
            CultureInfo.InvariantCulture,
            $"The request to '{requestUri}' failed with status {(int)response.StatusCode}{(reason is null ? null : $" {reason}")}.{(detail is null ? null : $" {detail}")}{(hint is null ? null : $" {hint}")}");

        return Trace(new MediaWikiException(message, response.StatusCode, error?.ErrorKey, GetRetryAfter(response)));
    }

    /// <summary>Throws a <see cref="MediaWikiException"/> describing the response unless it succeeded.</summary>
    private static async Task EnsureSuccessAsync(string requestUri, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);

        throw CreateException(requestUri, response, error);
    }

    /// <summary>
    /// Explains a <c>404</c> that did not come from a missing page, revision or file, since the status alone reads as
    /// "not found" when the real problem is the wiki or the base URL.
    /// </summary>
    private static string? GetNotFoundHint(HttpResponseMessage response, MediaWikiError? error)
    {
        return response.StatusCode is not HttpStatusCode.NotFound
            ? null
            : error?.ErrorKey switch
            {
                null => "The body was not a MediaWiki error, so the request may not have reached the REST API; check the base URL.",
                "rest-no-match" => "The wiki has no such endpoint, which usually means its MediaWiki version predates it.",
                "rest-prefix-mismatch" or "rest-unknown-module" => "The REST API did not recognize the path; check the base URL.",
                _ => null
            };
    }

    /// <summary>The value the history endpoint expects for a filter.</summary>
    private static string GetQueryValue(MediaWikiPageHistoryFilter filter)
    {
        return filter switch
        {
            MediaWikiPageHistoryFilter.Anonymous => "anonymous",
            MediaWikiPageHistoryFilter.Bot => "bot",
            MediaWikiPageHistoryFilter.Reverted => "reverted",
            MediaWikiPageHistoryFilter.Minor => "minor",
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, $"Not a {nameof(MediaWikiPageHistoryFilter)} this library knows.")
        };
    }

    /// <summary>The path segment the history count endpoint expects for a tally.</summary>
    private static string GetQueryValue(MediaWikiPageHistoryCountType type)
    {
        return type switch
        {
            MediaWikiPageHistoryCountType.Anonymous => "anonymous",
            MediaWikiPageHistoryCountType.Temporary => "temporary",
            MediaWikiPageHistoryCountType.Bot => "bot",
            MediaWikiPageHistoryCountType.Editors => "editors",
            MediaWikiPageHistoryCountType.Edits => "edits",
            MediaWikiPageHistoryCountType.Minor => "minor",
            MediaWikiPageHistoryCountType.Reverted => "reverted",
            MediaWikiPageHistoryCountType.AnonymousEdits => "anonedits",
            MediaWikiPageHistoryCountType.BotEdits => "botedits",
            MediaWikiPageHistoryCountType.RevertedEdits => "revertededits",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Not a {nameof(MediaWikiPageHistoryCountType)} this library knows.")
        };
    }

    /// <summary>The delay advertised by the <c>Retry-After</c> header, in either of its two forms.</summary>
    private static TimeSpan? GetRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;

        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var remaining = date - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        return null;
    }

    /// <summary>
    /// Tells a response that reports the page, revision or file missing from every other outcome: success passes,
    /// and any other failure, including a <c>404</c> for an endpoint the wiki does not serve, throws.
    /// </summary>
    /// <param name="requestUri">The endpoint that was fetched, for the exception message.</param>
    /// <param name="response">The response to judge.</param>
    /// <param name="cancellationToken">Token used to cancel reading the body.</param>
    /// <param name="absentErrorKey">An error key that also counts as absence for this endpoint, whatever its status.</param>
    /// <returns><see langword="true"/> if the wiki does not have what was asked for; <see langword="false"/> if the response succeeded.</returns>
    private static async Task<bool> IsAbsentAsync(
        string requestUri,
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        string? absentErrorKey = null)
    {
        if (response.IsSuccessStatusCode)
        {
            return false;
        }

        var error = await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);

        if (error?.ErrorKey is { } errorKey &&
            (errorKey == absentErrorKey || (response.StatusCode is HttpStatusCode.NotFound && AbsentResourceErrorKeys.Contains(errorKey))))
        {
            // Not a failure, but worth seeing in a trace: the span would otherwise read as a plain success.
            CurrentActivity?.SetTag(ErrorKeyTag, errorKey);

            return true;
        }

        throw CreateException(requestUri, response, error);
    }

    /// <summary>Reads the MediaWiki error body, or returns <see langword="null"/> if it is absent or unreadable.</summary>
    private static async Task<MediaWikiError?> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync(MediaWikiErrorJsonSerializerContext.Default.MediaWikiError, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or HttpRequestException or IOException)
        {
            // A wiki behind a proxy may answer with HTML, or with nothing; the status code still carries the failure.
            return null;
        }
    }

    /// <summary>Reads the response body, translating malformed or empty payloads into <see cref="MediaWikiException"/>.</summary>
    private static async Task<T> ReadJsonAsync<T>(
        string requestUri,
        HttpResponseMessage response,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        T? value;

        try
        {
            value = await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or HttpRequestException or IOException)
        {
            throw Trace(new MediaWikiException(
                $"The response to '{requestUri}' could not be read as {typeof(T).Name}: {exception.Message}",
                response.StatusCode,
                innerException: exception));
        }

        return value ?? throw Trace(new MediaWikiException(
            $"The response to '{requestUri}' was empty; expected {typeof(T).Name}.",
            response.StatusCode));
    }

    /// <summary>Marks the operation's activity as failed by <paramref name="exception"/>, and hands it back to be thrown.</summary>
    /// <remarks>
    /// The tag names follow the OpenTelemetry semantic conventions where one exists. The caller's own cancellation
    /// is not recorded: the activity simply ends, as the caller asked.
    /// </remarks>
    private static MediaWikiException Trace(MediaWikiException exception)
    {
        if (CurrentActivity is not { } activity)
        {
            return exception;
        }

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag(ErrorTypeTag, exception.GetType().FullName);

        if (exception.StatusCode is { } statusCode)
        {
            activity.SetTag(HttpResponseStatusCodeTag, (int)statusCode);
        }

        if (exception.ErrorKey is { } errorKey)
        {
            activity.SetTag(ErrorKeyTag, errorKey);
        }

        return exception;
    }

    /// <summary>Sends an edit and reads back the page the wiki stored.</summary>
    /// <remarks>
    /// Both endpoints answer with the same page representation <see cref="GetPageAsync(string, CancellationToken)"/>
    /// returns, under 200 for an edit and 201 for a creation.
    /// </remarks>
    /// <param name="requestUri">The endpoint the edit is sent to.</param>
    /// <param name="method">The verb the endpoint answers to.</param>
    /// <param name="request">The body describing the edit.</param>
    /// <param name="typeInfo">The metadata for the record the body serializes from.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <param name="operation">The public method being served, which names the activity; supplied by the compiler.</param>
    private async Task<MediaWikiPage> EditPageAsync<TRequest>(
        string requestUri,
        HttpMethod method,
        TRequest request,
        JsonTypeInfo<TRequest> typeInfo,
        CancellationToken cancellationToken,
        [CallerMemberName] string operation = "")
    {
        using var activity = StartActivity(operation);
        using var response = await SendJsonAsync(method, requestUri, request, typeInfo, null, cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(requestUri, response, cancellationToken).ConfigureAwait(false);

        return await ReadJsonAsync(requestUri, response, MediaWikiJsonSerializerContext.Default.MediaWikiPage, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends a GET and reads the body, translating transport failures and timeouts into <see cref="MediaWikiException"/>.</summary>
    private Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken)
    {
        return SendAsync(HttpMethod.Get, requestUri, null, null, cancellationToken);
    }

    /// <summary>Fetches a JSON response, failing rather than reporting a missing resource as an absent one.</summary>
    /// <param name="requestUri">The endpoint to fetch.</param>
    /// <param name="typeInfo">The metadata for the record the response deserializes into.</param>
    /// <param name="operation">The public method being served, which names the activity.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    private async Task<T> GetJsonAsync<T>(string requestUri, JsonTypeInfo<T> typeInfo, string operation, CancellationToken cancellationToken)
    {
        using var activity = StartActivity(operation);
        using var response = await GetAsync(requestUri, cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(requestUri, response, cancellationToken).ConfigureAwait(false);

        return await ReadJsonAsync(requestUri, response, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Fetches a JSON response, treating a missing page, revision or file as <see langword="null"/>.</summary>
    /// <param name="requestUri">The endpoint to fetch.</param>
    /// <param name="typeInfo">The metadata for the record the response deserializes into.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <param name="absentErrorKey">An error key that also counts as absence for this endpoint, whatever its status.</param>
    /// <param name="operation">The public method being served, which names the activity; supplied by the compiler.</param>
    private async Task<T?> GetJsonOrNullAsync<T>(
        string requestUri,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken,
        string? absentErrorKey = null,
        [CallerMemberName] string operation = "")
        where T : class
    {
        using var activity = StartActivity(operation);
        using var response = await GetAsync(requestUri, cancellationToken).ConfigureAwait(false);

        return await IsAbsentAsync(requestUri, response, cancellationToken, absentErrorKey).ConfigureAwait(false)
            ? null
            : await ReadJsonAsync(requestUri, response, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Fetches a response that carries markup rather than JSON, treating a missing one as <see langword="null"/>.</summary>
    /// <param name="requestUri">The endpoint to fetch.</param>
    /// <param name="accept">The representation to ask for; see <see cref="SendAsync"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <param name="operation">The public method being served, which names the activity; supplied by the compiler.</param>
    private async Task<string?> GetTextOrNullAsync(
        string requestUri,
        MediaTypeWithQualityHeaderValue accept,
        CancellationToken cancellationToken,
        [CallerMemberName] string operation = "")
    {
        using var activity = StartActivity(operation);
        using var response = await SendAsync(HttpMethod.Get, requestUri, null, accept, cancellationToken).ConfigureAwait(false);

        return await IsAbsentAsync(requestUri, response, cancellationToken).ConfigureAwait(false)
            ? null
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Runs one of the two search endpoints, which take the same arguments and answer in the same shape.</summary>
    /// <param name="endpoint">The endpoint to search.</param>
    /// <param name="query">The search term.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <param name="operation">The public method being served, which names the activity; supplied by the compiler.</param>
    private async Task<IReadOnlyList<MediaWikiSearchResult>> SearchAsync(
        string endpoint,
        string query,
        int limit,
        CancellationToken cancellationToken,
        [CallerMemberName] string operation = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxSearchLimit);

        var requestUri = string.Create(CultureInfo.InvariantCulture, $"{endpoint}?q={Uri.EscapeDataString(query)}&limit={limit}");

        var searchResponse = await GetJsonAsync(requestUri, MediaWikiJsonSerializerContext.Default.MediaWikiSearchResponse, operation, cancellationToken)
            .ConfigureAwait(false);

        return searchResponse.Pages;
    }

    /// <summary>Sends the request and reads the body, translating transport failures and timeouts into <see cref="MediaWikiException"/>.</summary>
    /// <remarks>
    /// <paramref name="accept"/> is set for the endpoints that answer with something other than JSON. The
    /// <see cref="HttpClient"/> that <c>AddMediaWikiClient</c> configures asks for <c>application/json</c> by default,
    /// which the wikitext-to-HTML transform refuses with <c>406</c>; a header set on the request replaces the default.
    /// </remarks>
    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string requestUri,
        HttpContent? content,
        MediaTypeWithQualityHeaderValue? accept,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, new Uri(requestUri, UriKind.Relative));
        request.Content = content;

        if (accept is not null)
        {
            request.Headers.Accept.Add(accept);
        }

        try
        {
            return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient reports its own timeout as a cancellation, even though the caller never asked to cancel.
            throw Trace(new MediaWikiException(
                string.Create(CultureInfo.InvariantCulture, $"The request to '{requestUri}' timed out after {_httpClient.Timeout}."),
                innerException: exception));
        }
        catch (HttpRequestException exception)
        {
            // The body is buffered here too, so this covers a request that never left as well as a truncated response.
            throw Trace(new MediaWikiException(
                $"The request to '{requestUri}' failed: {exception.Message}",
                exception.StatusCode,
                innerException: exception));
        }
    }

    /// <summary>Sends a request whose body is a JSON record.</summary>
    /// <remarks>
    /// The body is serialized up front rather than streamed: a streamed body goes out chunked, without a
    /// Content-Length, and MediaWiki answers that with <c>rest-request-body-expected</c> as if the request had carried none.
    /// </remarks>
    private async Task<HttpResponseMessage> SendJsonAsync<TRequest>(
        HttpMethod method,
        string requestUri,
        TRequest request,
        JsonTypeInfo<TRequest> typeInfo,
        MediaTypeWithQualityHeaderValue? accept,
        CancellationToken cancellationToken)
    {
        using var content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(request, typeInfo));
        content.Headers.ContentType = JsonMediaType;

        return await SendAsync(method, requestUri, content, accept, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts the activity for one operation, or returns <see langword="null"/> when nobody subscribed to
    /// <see cref="ActivitySource"/>. The activity is named after the public method, without its <c>Async</c> suffix,
    /// and tagged with the wiki's host so wikis stay apart when several are registered.
    /// </summary>
    private Activity? StartActivity(string operation)
    {
        // Checked first so the common, unobserved case does not pay for the name.
        if (!ActivitySource.HasListeners())
        {
            return null;
        }

        // Every operation ends in "Async", which says nothing in a trace.
        var activity = ActivitySource.StartActivity(operation[..^"Async".Length], ActivityKind.Client);

        // The constructor refuses a client without a base address.
        activity?.SetTag(ServerAddressTag, _httpClient.BaseAddress!.Host);

        return activity;
    }

    /// <summary>Posts content to a transform endpoint and reads back the converted markup.</summary>
    /// <param name="requestUri">The endpoint the content is sent to.</param>
    /// <param name="request">The body carrying the content to convert.</param>
    /// <param name="typeInfo">The metadata for the record the body serializes from.</param>
    /// <param name="accept">The representation to ask for; see <see cref="SendAsync"/>.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <param name="operation">The public method being served, which names the activity; supplied by the compiler.</param>
    private async Task<string> TransformAsync<TRequest>(
        string requestUri,
        TRequest request,
        JsonTypeInfo<TRequest> typeInfo,
        MediaTypeWithQualityHeaderValue accept,
        CancellationToken cancellationToken,
        [CallerMemberName] string operation = "")
    {
        using var activity = StartActivity(operation);
        using var response = await SendJsonAsync(HttpMethod.Post, requestUri, request, typeInfo, accept, cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(requestUri, response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }
}
