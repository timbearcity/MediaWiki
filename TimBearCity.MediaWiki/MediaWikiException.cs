using System.Net;

namespace TimBearCity.MediaWiki;

/// <summary>
/// The error thrown when a MediaWiki request does not produce a usable response.
/// </summary>
/// <remarks>
/// Derives from <see cref="HttpRequestException"/> so existing HTTP error handling keeps working, and adds the
/// detail the MediaWiki REST API returns in its error body.
/// </remarks>
/// <remarks>Creates an exception describing a failed MediaWiki request.</remarks>
/// <param name="message">A description of the failure.</param>
/// <param name="statusCode">The response status code, or <see langword="null"/> if no response was received.</param>
/// <param name="errorKey">The MediaWiki <c>errorKey</c>, if the response carried one.</param>
/// <param name="retryAfter">How long to wait before retrying, if the response carried a <c>Retry-After</c> header.</param>
/// <param name="innerException">The underlying failure, if any.</param>
public sealed class MediaWikiException(
    string message,
    HttpStatusCode? statusCode = null,
    string? errorKey = null,
    TimeSpan? retryAfter = null,
    Exception? innerException = null) : HttpRequestException(message, innerException, statusCode)
{
    /// <summary>
    /// The machine-readable MediaWiki error key, e.g. <c>rest-nonexistent-title</c>, or <see langword="null"/> if the
    /// response did not carry one.
    /// </summary>
    public string? ErrorKey { get; } = errorKey;

    /// <summary>
    /// Whether retrying the same request later may succeed: a timeout, a transport failure, a rate limit, or a
    /// server-side error. A response that exceeded <see cref="MediaWikiOptions.MaxResponseSize"/> is not one: the
    /// wiki answered, and would answer the same way again.
    /// </summary>
    public bool IsTransient => StatusCode switch
    {
        null => InnerException is not HttpRequestException { HttpRequestError: HttpRequestError.ConfigurationLimitExceeded },
        HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout => true,
        >= HttpStatusCode.InternalServerError => true,
        _ => false
    };

    /// <summary>
    /// How long to wait before retrying, taken from the <c>Retry-After</c> response header. Typically present on
    /// <see cref="HttpStatusCode.TooManyRequests"/> and <see cref="HttpStatusCode.ServiceUnavailable"/>.
    /// </summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
