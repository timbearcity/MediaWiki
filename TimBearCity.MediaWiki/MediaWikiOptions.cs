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
    /// Optional source of the bearer token, asked once per request, for a token that expires or differs per user.
    /// Answering <see langword="null"/> or whitespace sends the request anonymously. Cannot be combined with
    /// <see cref="AccessToken"/>, and cannot be bound from configuration.
    /// </summary>
    public Func<CancellationToken, ValueTask<string?>>? AccessTokenProvider { get; set; }

    /// <summary>
    /// Base URL of the MediaWiki REST endpoint (e.g., "https://en.wikipedia.org/w/rest.php/v1/"). Must be absolute, with an
    /// http or https scheme.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional upper bound, in bytes, on the size of a response body. Leave unset to keep the
    /// <see cref="HttpClient"/> default. A response that exceeds it fails with an
    /// <see cref="HttpRequestException"/> rather than being truncated.
    /// </summary>
    public int? MaxResponseSize { get; set; }

    /// <summary>
    /// Request timeout duration.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Mandatory HTTP User-Agent header required by MediaWiki policy.
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;
}
