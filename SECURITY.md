# Security policy

## Supported versions

Security fixes are released for the latest minor version on NuGet. Older versions are not patched; upgrade to receive the fix.

| Version | Supported |
|---------|-----------|
| latest  | yes       |
| older   | no        |

## Reporting a vulnerability

Please do not open a public issue for a security problem.

Report it privately through
[GitHub's private vulnerability reporting](https://github.com/timbearcity/MediaWiki/security/advisories/new) for this repository. Include the version affected,
what the problem is, and how to reproduce it.

You can expect an acknowledgment within 7 days, and a fix or a decision on the report within 90 days of the acknowledgment. Once a fix has shipped, the advisory
is published with credit to the reporter unless you ask otherwise.

## Scope

This library is an HTTP client for the MediaWiki REST API. What it is responsible for:

- Sending the `AccessToken` only to the `BaseUrl` it was configured with, over whatever `HttpClient` pipeline the host registered.
- Sending a `csrfToken` only in the body of the one write it was passed to, and not keeping it afterward.
- Never logging or including either token in exception messages; a `MediaWikiException` carries the request URI and the response, not the request body.
- Deserializing responses without executing or evaluating any content the wiki returns.

What is out of scope, and belongs to the host application:

- The safety of the wikitext or HTML the wiki returns. `GetPageHtmlAsync` and the transform endpoints hand back markup as it arrives; sanitize it before
  rendering it in a browser.
- Storage of the access token. The library reads it from `MediaWikiOptions`; keeping it out of source control and logs is the host's job.
- Obtaining a CSRF token, and the session cookies it is bound to. Both come from the action API, which this library does not cover.
- Transport security. Use an `https://` `BaseUrl`; the library does not refuse plain `http://`.

## Dependencies

Dependency vulnerabilities are checked on every restore by NuGet audit (`NuGetAuditMode=all`), and CI fails on any advisory of any severity.
