# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). Until 1.0.0, a minor version may contain breaking changes; each one is called out under **Changed**
or **Removed** so that upgrading is a matter of reading this file.

The public surface is tracked in `TimBearCity.MediaWiki/PublicAPI.Shipped.txt` and `TimBearCity.MediaWiki/PublicAPI.Unshipped.txt`, and package validation
compares each release against the previous one, so a change to the contract cannot ship without appearing here.

## [Unreleased]

### Changed

- `MediaWikiRevisionReference.Timestamp` is now `DateTimeOffset?`. Pages in the `MediaWiki:` namespace come back from Wikimedia wikis with a placeholder
  `latest` of `{"id": 0, "timestamp": null}`, which the non-nullable property could not hold.

### Fixed

- `GetPageAsync`, `GetPageBareAsync` and `GetPageWithHtmlAsync` no longer throw for pages in the `MediaWiki:` namespace, such as `MediaWiki:Common.css`
  ([#6](https://github.com/timbearcity/MediaWiki/issues/6)).
- `GetPageHistoryCountAsync` no longer fails with `400 Bad Request` when the key contains spaces, such as `Albert Einstein`
  ([#7](https://github.com/timbearcity/MediaWiki/issues/7)). The page endpoints now send spaces as underscores, the key form MediaWiki stores, so every page
  request lands directly instead of behind a `301` to the normalized title.
- `GetPageHistoryAsync` and `GetPageHistoryCountAsync` now throw `MediaWikiException` with `404 Not Found`, rather than returning `null`, when
  `olderThan`, `newerThan`, `fromRevisionId` or `toRevisionId` names a revision that does not exist or is not a revision of the page
  ([#8](https://github.com/timbearcity/MediaWiki/issues/8)). `null` again means only that the page does not exist. Each endpoint now treats only the
  error keys for its own resource as absence, so a revision endpoint no longer returns `null` for a page key either.
- `MediaWikiException.IsTransient` is now `false` for a `500` whose error key blames the request rather than the wiki:
  `rest-pagehistorycount-too-many-revisions`, which `GetPageHistoryCountAsync` gets for a `Minor` count on a page of more than 2000 edits, and
  `rest-search-error`, which the search methods get for a query the engine rejects, such as a malformed `insource:` regular expression
  ([#9](https://github.com/timbearcity/MediaWiki/issues/9)). A retry policy keyed on `IsTransient` no longer repeats either.
- The bearer token from `AccessToken` or `AccessTokenProvider` is no longer dropped on a redirect that stays on the wiki, such as the `307` the HTML and
  lint endpoints answer a redirect page with, or the `301` to a normalized title ([#10](https://github.com/timbearcity/MediaWiki/issues/10)). The client
  now follows redirects itself, above the handler that sets the token, instead of leaving them to `HttpClientHandler`, which strips `Authorization` from
  every redirected request; `AllowAutoRedirect` is turned off on the primary handler, including one supplied through `ConfigurePrimaryHttpMessageHandler`.
  A redirect to another scheme, host or port still goes out without the token, and a redirect from `https` to `http` is not followed.

## [0.1.0] - 2026-09-15

### Added

- `IMediaWikiClient` and `MediaWikiClient`, covering every documented endpoint of the MediaWiki core REST API (`/w/rest.php/v1/`): pages, page history and
  history counts, language links, revisions and revision comparison, search, files, page creation and update, and the wikitext, HTML and lint transforms. Every
  method that names one page, revision or file returns `null` when the wiki does not have it and throws for everything else; the write methods throw in both
  cases.
- `AddMediaWikiClient` registration over `IHttpClientFactory`, in the `Microsoft.Extensions.DependencyInjection` namespace, with one registration per wiki,
  keyed registrations for further wikis, and options binding from configuration.
- `MediaWikiOptions` with `BaseUrl`, `UserAgent`, `Timeout`, `MaxResponseSize`, and bearer authentication through `AccessToken` or a per-request
  `AccessTokenProvider`.
- `MediaWikiException`, derived from `HttpRequestException`, carrying the status code, MediaWiki's error key, the `Retry-After` delay and an
  `IsTransient` flag.
- Records for every response shape, with enums for the history filter, history count type and diff vocabulary. The records live in the
  `TimBearCity.MediaWiki.Pages`, `.Revisions`, `.Files`, `.Search` and `.Transform` namespaces, with the types shared across them in
  `TimBearCity.MediaWiki.Common`; the client, options and exception stay in `TimBearCity.MediaWiki`.
- An `ActivitySource` named `TimBearCity.MediaWiki` (`MediaWikiClient.ActivitySourceName`), tracing each operation as a parent of the `HttpClient`
  request with the wiki's host and, on failure, the status code, MediaWiki's error key and the exception type.
- Native AOT and trimming support through a System.Text.Json source generator context.
- Targets `net8.0` and `net10.0`.

[Unreleased]: https://github.com/timbearcity/MediaWiki/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/timbearcity/MediaWiki/releases/tag/v0.1.0
