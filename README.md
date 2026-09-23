# TimBearCity.MediaWiki

[![CI](https://github.com/timbearcity/MediaWiki/actions/workflows/ci.yml/badge.svg)](https://github.com/timbearcity/MediaWiki/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/TimBearCity.MediaWiki.svg)](https://www.nuget.org/packages/TimBearCity.MediaWiki)
[![Coverage](https://img.shields.io/badge/coverage-100%25-brightgreen)](https://github.com/timbearcity/MediaWiki/actions/workflows/ci.yml)
[![Docs](https://img.shields.io/badge/docs-API%20reference-blue)](https://timbearcity.github.io/MediaWiki/)

An unofficial .NET client for the [MediaWiki REST API](https://www.mediawiki.org/wiki/API:REST_API) (`/w/rest.php/v1/`), covering every documented endpoint of
the core API. This file is the guide; the [API reference](https://timbearcity.github.io/MediaWiki/api/) lists every public type and member.

## Install

```
dotnet add package TimBearCity.MediaWiki
```

Targets `net8.0` and `net10.0`, and is trimming and Native AOT compatible: responses are deserialized through a System.Text.Json source generator, and
configuration is bound by hand rather than through reflection.

## Register

`AddMediaWikiClient` registers `IMediaWikiClient` as a typed `HttpClient`. A `User-Agent` is
[required by the MediaWiki API guidelines](https://foundation.wikimedia.org/wiki/Policy:Wikimedia_Foundation_User-Agent_Policy); the options are validated at
startup, and a `UserAgent` that is not a well-formed header is refused the first time the client is built.

```csharp
builder.Services.AddMediaWikiClient(options =>
{
    options.BaseUrl = "https://en.wikipedia.org/w/rest.php/v1/";
    options.UserAgent = "MyApp/1.0 (https://example.com; contact@example.com)";
});
```

Or bind a configuration section:

```csharp
builder.Services.AddMediaWikiClient(builder.Configuration.GetSection(MediaWikiOptions.Position));
```

```json
{
    "MediaWiki": {
        "BaseUrl": "https://en.wikipedia.org/w/rest.php/v1/",
        "UserAgent": "MyApp/1.0 (https://example.com; contact@example.com)"
    }
}
```

| Option                | Default    | Notes                                                                                                                                                                                                                                                     |
|-----------------------|------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `BaseUrl`             | none       | Required. Absolute `http` or `https` URL of the REST endpoint.                                                                                                                                                                                            |
| `UserAgent`           | none       | Required. Sent ahead of this library's own token.                                                                                                                                                                                                         |
| `Timeout`             | 30 seconds | Applied to the underlying `HttpClient`, so at most 24.20:31:23.647, or `Timeout.InfiniteTimeSpan` (`"-00:00:00.001"` in configuration) to let a resilience handler own the timeout. Bound as `d.hh:mm:ss`: `"30"` is 30 days, `"00:00:30"` is 30 seconds. |
| `MaxResponseSize`     | none       | Optional cap on a response body, in bytes. Unset keeps the `HttpClient` default (2 GB). A larger response throws rather than truncates.                                                                                                                   |
| `AccessToken`         | none       | Optional OAuth2 / personal access token, sent as a bearer token. Required to write.                                                                                                                                                                       |
| `AccessTokenProvider` | none       | Optional callback asked for the bearer token before each HTTP request, including each redirect hop and retry, so it should cache. Code only; exclusive with `AccessToken`.                                                                                |

Each registration binds one client to one wiki: `BaseUrl` is the `HttpClient` base address and `AccessToken` is only valid on that wiki. Registering the same
wiki twice throws from the second `AddMediaWikiClient` call.

## Multiple wikis

Pass a name to register further wikis. Each gets its own options, `HttpClient` and handler pipeline, and is resolved as a keyed service.

```csharp
builder.Services.AddMediaWikiClient(options => { /* the default wiki */ });

builder.Services.AddMediaWikiClient("commons", options =>
{
    options.BaseUrl = "https://commons.wikimedia.org/w/rest.php/v1/";
    options.UserAgent = "MyApp/1.0 (https://example.com; contact@example.com)";
});
```

```csharp
public sealed class ImageService(
    IMediaWikiClient wikipedia,
    [FromKeyedServices("commons")] IMediaWikiClient commons);
```

Configuration sections work the same way, so each wiki can be configured independently:

```csharp
builder.Services.AddMediaWikiClient("commons", builder.Configuration.GetSection("MediaWiki:Commons"));
```

The unnamed registration stays available as plain `IMediaWikiClient`, so single-wiki apps need no key.

For a wiki only known at runtime, such as a base URL read from a database, construct the client directly over an
`HttpClient` whose `BaseAddress` you have set. The address must end with `/` and have no query string or fragment, as `BaseUrl` must, and the
constructor throws otherwise. Nothing from the table above is applied on this path, so the `User-Agent` the policy asks for, and a bearer token if you
need one, go on the `HttpClient` yourself:

```csharp
httpClient.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/");
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MyApp/1.0 (https://example.com; contact@example.com)");

var client = new MediaWikiClient(httpClient);
```

## Use

```csharp
public sealed class WikiService(IMediaWikiClient client)
{
    public async Task<string?> GetSourceAsync(string key, CancellationToken cancellationToken)
    {
        MediaWikiPage? page = await client.GetPageAsync(key, cancellationToken);
        return page?.Source;
    }

    public async Task<IReadOnlyList<MediaWikiSearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
        => await client.SearchPagesAsync(query, limit: 10, cancellationToken);
}
```

The client, its options and the exception live in `TimBearCity.MediaWiki`. The records for each area of the API sit in a sub-namespace named for it:
`TimBearCity.MediaWiki.Pages`, `.Revisions`, `.Files`, `.Search` and `.Transform`, so the sample above also needs `using TimBearCity.MediaWiki.Pages;`
and `using TimBearCity.MediaWiki.Search;`. The two types those areas share (`MediaWikiUser` and `MediaWikiLicense`) are in `TimBearCity.MediaWiki.Common`.

Each endpoint has its own method, answering with a record that carries exactly the fields that endpoint returns. Every method that names one page, revision or
file answers `null` when the wiki does not have it, and throws for everything else; the write methods are the exception, since a page that could not be written
is a failure rather than an absent result.

"Does not have it" is decided by the error key in the `404` body, not by the status alone. MediaWiki also answers `404` for an endpoint it does not serve
(`rest-no-match`), and a web server answers `404` with HTML for a `BaseUrl` that misses the REST API; both throw, with the key and a hint in the message, so a
misconfigured client or an older wiki cannot pass for a wiki with no pages.

## Third-party wikis

The client speaks the core REST API, so it works against any MediaWiki, not only Wikimedia's. Three things differ from Wikipedia:

- **Version.** Most endpoints date from MediaWiki 1.35. `GetPageLintAsync` and `GetRevisionLintAsync` were added to core in October 2025, and
  `GetFileThumbnailsAsync` in May 2026, so the LTS release (1.43) serves neither; they throw `rest-no-match` there.
  `MediaWikiPageHistoryCountType.Temporary` needs a wiki with temporary accounts, and `AnonymousEdits`, `BotEdits` and `RevertedEdits` are deprecated aliases
  the wiki still accepts.
- **Authentication.** A bearer `AccessToken` is honored only where [Extension:OAuth](https://www.mediawiki.org/wiki/Extension:OAuth) is installed; it is not
  bundled with MediaWiki, and without it the header is ignored and the request is anonymous. The alternative is cookie authentication: log in through the action
  API, attach the resulting cookies to the `HttpClient` (a `CookieContainer` on the primary handler, via the `IHttpClientBuilder` that
  `AddMediaWikiClient` returns), and pass `csrfToken` to the write methods.
- **Extensions.** `Description` on a search result needs a provider such as Wikibase or ShortDescription, and `Thumbnail` needs PageImages; both are `null`
  otherwise.

The file endpoints return a mix of absolute and protocol-relative URLs (`//host/...`), sometimes within one response, so add the wiki's scheme only to a URL
that starts with `//` before handing it to `Uri`.

Point `BaseUrl` at the host `$wgServer` names. The HTML and lint endpoints redirect to absolute URLs built from it, for a redirect page and for title
normalization. The client follows redirects itself rather than leaving them to `HttpClientHandler`, which strips the bearer token from every redirected
request, so a hop that stays on the wiki keeps the token. A hop to another scheme, host or port goes out without `Authorization` or `Cookie`, whether the
client set them or you did, since they were meant for the wiki: a reverse proxy or container that reaches the wiki under another name therefore answers
`403` on a private wiki, or not at all if that name only resolves inside the network. Such a hop is also only followed for a `GET`; a `307` or `308` off the
wiki for a write would re-send the body, CSRF token included, so it is reported as `MediaWikiException` instead. To make this work, `AllowAutoRedirect` is
turned off on the primary handler, including one supplied through `ConfigurePrimaryHttpMessageHandler`; a primary handler of another type must not follow
redirects on its own.

### Pages

| Method                      | Endpoint                           | Returns                                                                       |
|-----------------------------|------------------------------------|-------------------------------------------------------------------------------|
| `GetPageAsync`              | `page/{key}`                       | `MediaWikiPage`, with the wikitext `Source`                                   |
| `GetPageWithHtmlAsync`      | `page/{key}/with_html`             | `MediaWikiPageWithHtml`, with the rendered `Html`                             |
| `GetPageBareAsync`          | `page/{key}/bare`                  | `MediaWikiPageBare`, metadata only, with the `HtmlUrl` to fetch the HTML from |
| `GetPageHtmlAsync`          | `page/{key}/html`                  | the rendered HTML as a `string`, with no metadata                             |
| `GetPageHistoryAsync`       | `page/{key}/history`               | `MediaWikiPageHistory`, one page of revisions                                 |
| `GetPageHistoryCountAsync`  | `page/{key}/history/counts/{type}` | `MediaWikiPageHistoryCount`                                                   |
| `GetPageLanguageLinksAsync` | `page/{key}/links/language`        | `MediaWikiPageLanguageLink` list, this page on the sister wikis               |
| `GetPageFilesAsync`         | `page/{key}/links/media`           | `MediaWikiFile` list, the files the page uses                                 |
| `GetPageLintAsync`          | `page/{key}/lint`                  | `MediaWikiLintError` list, what the linter found in the markup                |

The three record-returning representations share the metadata on `MediaWikiPageMetadata`. `GetPageHtmlAsync` hands back the markup as it arrives, for the caller
to give to whichever HTML parser they prefer. `GetPageAsync` and `GetPageBareAsync` report a redirect page through `RedirectTarget`; `GetPageWithHtmlAsync`
follows the redirect, so it answers with the target's page. `License` is always present, but its `Title` and `Url` are `null` on a wiki that never set
`$wgRightsText` and `$wgRightsUrl`, which is the MediaWiki default; the same applies to the revision records.

A page key is escaped as a single path segment, so a subpage such as `Help:Contents/Editing` is requested as `Help:Contents%2FEditing`. Wikimedia's servers
decode that; a self-hosted Apache behind MediaWiki refuses it with `404` unless the virtual host sets `AllowEncodedSlashes NoDecode`, as the
[MediaWiki installation guide](https://www.mediawiki.org/wiki/Manual:Short_URL/Apache) recommends.

The wiki returns at most 20 revisions of history at a time. To walk further back, pass the identifier of the oldest revision you were given as `olderThan`; to
walk forward, pass the newest as `newerThan`. The `filter` and `type` arguments are enums rather than strings, so the wiki's vocabulary is discoverable and a
typo is a compile error. A count can be narrowed to a range of revisions with `fromRevisionId` and `toRevisionId`, given together, for `Edits` and `Editors`
only.

```csharp
MediaWikiPageHistory? history = await client.GetPageHistoryAsync(
    "Solar_System",
    filter: MediaWikiPageHistoryFilter.Bot,
    cancellationToken: cancellationToken);

MediaWikiPageHistory? older = await client.GetPageHistoryAsync(
    "Solar_System",
    olderThan: history!.Revisions[^1].Id,
    cancellationToken: cancellationToken);
```

### Revisions

| Method                     | Endpoint                       | Returns                                                                           |
|----------------------------|--------------------------------|-----------------------------------------------------------------------------------|
| `GetRevisionAsync`         | `revision/{id}`                | `MediaWikiRevision`, with the wikitext `Source`                                   |
| `GetRevisionWithHtmlAsync` | `revision/{id}/with_html`      | `MediaWikiRevisionWithHtml`, with the rendered `Html`                             |
| `GetRevisionBareAsync`     | `revision/{id}/bare`           | `MediaWikiRevisionBare`, metadata only, with the `HtmlUrl` to fetch the HTML from |
| `GetRevisionHtmlAsync`     | `revision/{id}/html`           | the rendered HTML as a `string`, with no metadata                                 |
| `GetRevisionLintAsync`     | `revision/{id}/lint`           | `MediaWikiLintError` list                                                         |
| `CompareRevisionsAsync`    | `revision/{from}/compare/{to}` | `MediaWikiRevisionComparison`, a line-based diff                                  |

These mirror the page representations, and share their metadata on `MediaWikiRevisionMetadata`. A page points at its latest revision through the smaller
`MediaWikiRevisionReference`, just as a revision points at its page through `MediaWikiPageReference`.

A comparison reports each line's `Type` as a `MediaWikiDiffType`, marks up the changed stretches of a `Changed` line through `HighlightRanges`, and links the
two halves of a moved paragraph through `MoveInfo`.

### Search

| Method              | Endpoint       | Returns                                                                               |
|---------------------|----------------|---------------------------------------------------------------------------------------|
| `SearchPagesAsync`  | `search/page`  | `MediaWikiSearchResult` list, matched on page content                                 |
| `SearchTitlesAsync` | `search/title` | `MediaWikiSearchResult` list, matched on title, for completing what someone is typing |

Both return an empty list when nothing matched, and accept a `limit` between 1 and
`MediaWikiClient.MaxSearchLimit` (100), defaulting to the API's own 50. A title search puts the title in `Excerpt`, where a full-text search puts a snippet of
the matching content.

### Files

| Method                   | Endpoint                  | Returns                                              |
|--------------------------|---------------------------|------------------------------------------------------|
| `GetFileAsync`           | `file/{title}`            | `MediaWikiFile`, with the renditions the wiki serves |
| `GetFileThumbnailsAsync` | `file/{title}/thumbnails` | `MediaWikiFileThumbnails`, the standard sizes        |

A wiki that draws on a shared repository, as Wikipedia does on Wikimedia Commons, answers for the files it borrows as well as the ones it hosts. The renditions
are nullable throughout: a media type the wiki has no preferred form for is reported as an absence rather than an error. In the same spirit,
`GetFileThumbnailsAsync` answers `null` for a file the wiki cannot produce thumbnails for, such as one of a media type it has no handler installed for, which
the API refuses with `400`. A wiki with the handler answers audio and video with thumbnails of the file-type icon, at a `Width` and `Height` of `0`.

## Write

Creating and updating pages needs an authenticated client: set `AccessToken` to an OAuth2 token or personal access token carrying the rights the wiki asks for,
or, for a token that expires or differs per user, `AccessTokenProvider`, which is asked before each HTTP request and sends it anonymously when it answers
`null`. That includes each hop of a redirect and each retry of a resilience handler, so a provider that fetches from a token service should cache the token.
Either one needs Extension:OAuth on the wiki; see [Third-party wikis](#third-party-wikis) for the cookie-based alternative.

```csharp
builder.Services.AddMediaWikiClient(options =>
{
    options.BaseUrl = "https://en.wikipedia.org/w/rest.php/v1/";
    options.UserAgent = "MyApp/1.0 (https://example.com; contact@example.com)";
    options.AccessTokenProvider = cancellationToken => tokens.GetAccessTokenAsync(cancellationToken);
});
```

```csharp
MediaWikiPage created = await client.CreatePageAsync(
    "Wikipedia:Sandbox/Example",
    source: "Hello, world.",
    comment: "Create the example page",
    cancellationToken: cancellationToken);

MediaWikiPage? page = await client.GetPageAsync("Wikipedia:Sandbox/Example", cancellationToken);

MediaWikiPage updated = await client.UpdatePageAsync(
    "Wikipedia:Sandbox/Example",
    source: page!.Source + "\n\nAnd another line.",
    latestRevisionId: page.Latest.Id,
    comment: "Add a line",
    cancellationToken: cancellationToken);
```

| Method            | Endpoint         | Returns                                                                  |
|-------------------|------------------|--------------------------------------------------------------------------|
| `CreatePageAsync` | `POST page`      | the created `MediaWikiPage`, carrying the revision the creation produced |
| `UpdatePageAsync` | `PUT page/{key}` | the stored `MediaWikiPage`, carrying the revision the edit produced      |

Both answer with the representation `GetPageAsync` returns.

`latestRevisionId` is the revision the edit was based on, and it decides what an update means:

| `latestRevisionId`    | Meaning            | Refused when                                                                     |
|-----------------------|--------------------|----------------------------------------------------------------------------------|
| the revision you read | Edit that revision | the page moved on since, with `409` and `editconflict` (`edit-conflict` on 1.43) |
| omitted               | Create the page    | the page already exists, with `409` and `rest-update-cannot-create-page`         |

So pass it whenever you are editing, and the wiki declines the edit instead of overwriting a change someone else made in the meantime. A page deleted since it
was read comes back as `404`, and one the account may not edit as `403`.

`comment` is the edit summary; the wiki insists on the field but accepts it empty. `contentModel` is optional, defaulting to whatever the wiki uses, normally
`wikitext`. `csrfToken` is only for the cookie-authenticated callers described under [Third-party wikis](#third-party-wikis).

## Transform

The transform endpoints convert content without saving it, which is what makes them useful for previewing an edit or checking it before it is made.

| Method                         | Endpoint                          | Returns                   |
|--------------------------------|-----------------------------------|---------------------------|
| `TransformWikitextToHtmlAsync` | `POST transform/wikitext/to/html` | the rendered HTML         |
| `TransformHtmlToWikitextAsync` | `POST transform/html/to/wikitext` | the converted wikitext    |
| `TransformWikitextToLintAsync` | `POST transform/wikitext/to/lint` | `MediaWikiLintError` list |

```csharp
string html = await client.TransformWikitextToHtmlAsync(
    "== Hello world ==",
    title: "Wikipedia:Sandbox",
    cancellationToken: cancellationToken);
```

Naming a `title` gives the content a page to be relative to, which is what decides how its links, templates and magic words resolve. Naming a `revisionId` as
well tells the wiki which revision the content came from, which is what makes an HTML round trip lossless: the parts that did not change keep their original
wikitext. A `revisionId` without a `title` is refused, since a revision means nothing without the page it belongs to.

## Errors

Every failure other than an absent page, revision or file throws `MediaWikiException`: a rejected request, a timeout, a connection failure, an unreadable body.
It derives from `HttpRequestException`, so existing HTTP error handling still catches it, and carries the detail MediaWiki returns:

| Member        | Notes                                                                                                                                                                                                                   |
|---------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `StatusCode`  | The response status, or `null` if no response arrived (timeout, DNS or TLS failure).                                                                                                                                    |
| `ErrorKey`    | MediaWiki's machine-readable key, e.g. `rest-no-match`, when the body carried one.                                                                                                                                      |
| `RetryAfter`  | The `Retry-After` delay, typically present on `429` and `503`.                                                                                                                                                          |
| `IsTransient` | `true` for a timeout, transport failure, rate limit or `5xx`; `false` for a response over `MaxResponseSize`, and for a `500` that blames the request (`rest-pagehistorycount-too-many-revisions`, `rest-search-error`). |

```csharp
try
{
    MediaWikiPage? page = await client.GetPageAsync(key, cancellationToken);
}
catch (MediaWikiException exception) when (exception.IsTransient)
{
    logger.LogWarning(exception, "Wiki unavailable; retry after {RetryAfter}.", exception.RetryAfter);
}
```

The client's own `Timeout` throws `MediaWikiException`; only canceling your own `CancellationToken` throws `OperationCanceledException`. Retries and circuit
breaking are left to the caller, so nothing is retried unless you add a resilience handler to the `IHttpClientBuilder` that `AddMediaWikiClient` returns. The
standard one from [`Microsoft.Extensions.Http.Resilience`](https://www.nuget.org/packages/Microsoft.Extensions.Http.Resilience) retries transient failures
with backoff, honors `Retry-After`, and opens a circuit breaker when the wiki keeps failing:

```csharp
builder.Services
    .AddMediaWikiClient(builder.Configuration.GetSection(MediaWikiOptions.Position))
    .AddStandardResilienceHandler();
```

The handler's timeouts and the client's own `Timeout` run side by side, and whichever expires first wins. To leave timing out to the handler alone, set
`Timeout` to `Timeout.InfiniteTimeSpan` (`"-00:00:00.001"` in configuration).

A handler you add is yours to catch. When the standard handler gives up, it throws its own exceptions rather than `MediaWikiException`:
`TimeoutRejectedException`
for its attempt and total timeouts, `BrokenCircuitException` while the circuit is open and `RateLimiterRejectedException` from its rate limiter. The operation's
`Activity` is still marked as failed.

```csharp
catch (MediaWikiException exception) when (exception.IsTransient)
{
    logger.LogWarning(exception, "Wiki unavailable; retry after {RetryAfter}.", exception.RetryAfter);
}
catch (Exception exception) when (exception is TimeoutRejectedException or BrokenCircuitException)
{
    logger.LogWarning(exception, "Wiki unavailable; the resilience handler gave up.");
}
```

## Diagnostics

Each operation runs inside an `Activity` from the `TimBearCity.MediaWiki` source (`MediaWikiClient.ActivitySourceName`), named after the method without its
`Async` suffix (`GetPage`, `SearchTitles`) and parenting the `HttpClient` request span. It carries `server.address`, so wikis stay apart when several are
registered, and on failure the status `Error` with `error.type`, `http.response.status_code` and `mediawiki.error_key`. A page, revision or file the wiki does
not have is not a failure, but its `mediawiki.error_key` is still recorded. Nothing is created without a subscriber:

```csharp
builder.Services
    .AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(MediaWikiClient.ActivitySourceName)
        .AddHttpClientInstrumentation());
```

Request and response logging is the `IHttpClientFactory` default: set the `System.Net.Http.HttpClient.IMediaWikiClient` category (or
`System.Net.Http.HttpClient.MediaWiki:<name>` for a named wiki) to `Information` for one line per request, or `Trace` for headers. The library adds no
`ILogger` of its own.

## Not wrapped

Every documented endpoint under `/v1/` has a method here, with three deliberate omissions. The `stash` and `flavor` query parameters of the HTML endpoints serve
VisualEditor's edit-stashing workflow rather than a general client. The `redirect` parameter is likewise left at the wiki's default throughout. And
`/v1/search` is undocumented, with no response schema published, so there is nothing stable to model.

MediaWiki also serves a few REST modules outside `/v1/`, none of them meant for a client like this one: `site/v1` (XML sitemaps for crawlers), `specs/v0`
(the API's own OpenAPI descriptions) and `fragments/v0-internal` (HTML fragments for the skin).

Deleting, moving and undeleting pages, and logging in, have no REST equivalent at all: they live in the action API (`/w/api.php`), which this library does not
cover.

## Coverage

The suite covers every line and branch of the library. To reproduce the measurement:

```
dotnet test --framework net10.0 -- --coverage --coverage-settings TimBearCity.MediaWiki.Tests/CodeCoverage.config \
    --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
```

`TimBearCity.MediaWiki.Tests/CodeCoverage.config` narrows the run to `TimBearCity.MediaWiki.dll` and drops the sources under `obj/`, so what the JSON source
generator emits does not count toward the result. The report lands in `TestResults/`.

The suite itself runs on every target framework the library ships for; coverage is a property of the source, so it is measured once, on the newest one.

CI runs the same command on every push and pull request, then gates the build on the result:

```
python3 .github/scripts/check-coverage.py --minimum 100
```

The threshold lives in `MINIMUM_COVERAGE` in `.github/workflows/ci.yml`, and the run uploads the Cobertura report as the `coverage-cobertura` artifact. Anything
below the threshold fails the build, so a change that drops a line or a branch has to come with the test that covers it.

The coverage badge at the top of this file is a fixed number rather than a live one, which is what the gate makes safe: coverage cannot fall below the threshold
without the build failing. To keep the two from drifting apart, the same step also checks that the badge and `MINIMUM_COVERAGE` agree, so changing one without
the other fails CI.

## Smoke tests

Everything above runs against canned responses. The record shapes were modeled from the API documentation, so a field a wiki omits, renames or serves in an
unexpected form would otherwise show up for the first time in an application. `MediaWikiClientReadSmokeTests` closes that gap: it reads from a live wiki through
`AddMediaWikiClient` and checks that every read endpoint deserializes and that ids agree across them. Nothing in it writes.

The tests are skipped unless `MEDIAWIKI_SMOKE_READ_BASE_URL` names the REST API root to read from:

```
MEDIAWIKI_SMOKE_READ_BASE_URL=https://en.wikipedia.org/w/rest.php/v1/ dotnet test --framework net10.0 -- --filter-class "*ReadSmokeTests"
```

`MEDIAWIKI_SMOKE_READ_PAGE` and `MEDIAWIKI_SMOKE_READ_FILE` choose the page and file to read, and default to `Albert_Einstein` and `File:Wiki.png`, which exist
on the English Wikipedia. Pointing them at a page with a long history exercises every test; a fresh wiki with a single-revision page skips the comparison and
pagination tests, and a wiki whose version predates an endpoint (see [Third-party wikis](#third-party-wikis)) skips the tests for it rather than failing them.

CI runs the read tests against the English Wikipedia once a week, and on demand from the **Run workflow** button with **smoke** ticked. That run is kept out of
the push and pull request runs, whose result should depend on nothing but the code; its job is to catch a change on Wikimedia's side, so when the weekly run
fails it opens an issue, or comments on the open one, rather than waiting for someone to look at the Actions tab.

### Writes

The read tests cannot cover `CreatePageAsync` and `UpdatePageAsync` without editing the wiki they read from, so `MediaWikiClientWriteSmokeTests` edits a
different one: a wiki that exists only for the run. It creates a page under a fresh title for each test, updates it, and checks the conflict paths: creating a
title that exists, updating from a stale revision, and updating without a revision where a page already exists. The pages are never cleaned up, which is why the
target is its own opt-in, `MEDIAWIKI_SMOKE_WRITE_BASE_URL`, and never `MEDIAWIKI_SMOKE_READ_BASE_URL`.

`.github/scripts/start-throwaway-wiki.sh` starts such a wiki from the official Docker image, on SQLite inside the container, with anonymous editing switched on,
seeds it with a page, a file and a redirect for the read tests, and prints its REST API root. The image lacks two things the read tests need: the `wikidiff2`
PHP extension, without which the revision comparison endpoint answers `500`, and linting, without which the lint endpoints answer nothing. The script builds the
first from a source release pinned by version and checksum, and switches the second on. Its argument picks the MediaWiki version, `1.43` (the default)
or `1.46`, each pinned by digest in the script:

```
base_url=$(bash .github/scripts/start-throwaway-wiki.sh 1.46)
MEDIAWIKI_SMOKE_READ_BASE_URL=$base_url MEDIAWIKI_SMOKE_READ_PAGE=Smoke_page MEDIAWIKI_SMOKE_READ_FILE=File:Smoke.png MEDIAWIKI_SMOKE_WRITE_BASE_URL=$base_url dotnet test --framework net10.0 -- --filter-class "*SmokeTests"
docker rm --force smoke-wiki
```

Under Git Bash on Windows, prefix the script with `MSYS_NO_PATHCONV=1`, or the paths it passes into the container are rewritten to Windows ones.

CI does the same on every push and pull request, once against 1.43 (the oldest supported LTS) and once against 1.46 (the current stable release), so every
change is proven against a real MediaWiki for reads and writes alike, across the range of versions the client is meant to work with. Unlike the Wikipedia run,
these depend on nothing outside the repository but the pinned image, the pinned `wikidiff2` release and the Debian package the build needs. The state of a real
wiki cannot sway their result.

The edits are anonymous, using MediaWiki's logged-out CSRF token. To write as a user instead, set `MEDIAWIKI_SMOKE_WRITE_ACCESS_TOKEN` to an OAuth2 or personal
access token for that wiki.

## Contributing

Commit messages follow Conventional Commits; the types, the two Git settings to apply after cloning, and how the check runs are in
[CONTRIBUTING.md](CONTRIBUTING.md).

## License

MIT. This project is not affiliated with or endorsed by the Wikimedia Foundation.
