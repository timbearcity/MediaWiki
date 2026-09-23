namespace TimBearCity.MediaWiki;

/// <summary>Configuration for <see cref="IMediaWikiClient"/>.</summary>
public sealed class MediaWikiOptions
{
    /// <summary>The default configuration section name.</summary>
    public const string Position = "MediaWiki";

    /// <summary>
    /// Optional OAuth2 / Personal Access Token for authenticated requests. Sent as a bearer token on every request,
    /// so it suits a token that does not expire. Cannot be combined with <see cref="AccessTokenProvider"/>.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Optional source of the bearer token, for a token that expires or differs per user. It is asked before each HTTP
    /// request, including each hop of a redirect and each retry, so a provider that fetches the token should cache it.
    /// Answering <see langword="null"/> or whitespace sends the request anonymously. Cannot be combined with
    /// <see cref="AccessToken"/>, and cannot be bound from configuration.
    /// </summary>
    public Func<CancellationToken, ValueTask<string?>>? AccessTokenProvider { get; set; }

    /// <summary>
    /// Base URL of the MediaWiki REST endpoint (e.g., "https://en.wikipedia.org/w/rest.php/v1/"). Must be absolute, with an
    /// http or https scheme, its path must end with <c>/</c>, and it must have no query string or fragment.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional upper bound, in bytes, on the size of a response body. Leave unset to keep the
    /// <see cref="HttpClient"/> default. A response that exceeds it fails with an
    /// <see cref="HttpRequestException"/> rather than being truncated.
    /// </summary>
    public int? MaxResponseSize { get; set; }

    /// <summary>
    /// Request timeout duration. Must be greater than zero and at most 24.20:31:23.647, the most
    /// <see cref="HttpClient.Timeout"/> accepts, or <see cref="Timeout.InfiniteTimeSpan"/> to let a
    /// resilience handler own the timeout instead. A configuration value is read as <c>d.hh:mm:ss</c>, so <c>"30"</c>
    /// is 30 days; write 30 seconds as <c>"00:00:30"</c> and the infinite timeout as <c>"-00:00:00.001"</c>.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Mandatory HTTP User-Agent header required by MediaWiki policy.
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;
}
