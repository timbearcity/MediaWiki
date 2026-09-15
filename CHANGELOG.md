# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). Until 1.0.0, a minor version may contain breaking changes; each one is called out under **Changed**
or **Removed** so that upgrading is a matter of reading this file.

The public surface is tracked in `TimBearCity.MediaWiki/PublicAPI.Shipped.txt` and `TimBearCity.MediaWiki/PublicAPI.Unshipped.txt`, and package validation
compares each release against the previous one, so a change to the contract cannot ship without appearing here.

## [Unreleased]

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

[Unreleased]: https://github.com/timbearcity/MediaWiki/commits/main
