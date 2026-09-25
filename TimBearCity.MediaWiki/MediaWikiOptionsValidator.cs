using Microsoft.Extensions.Options;

namespace TimBearCity.MediaWiki;

/// <summary>
/// Checks <see cref="MediaWikiOptions"/> for both ways of building a client: <c>AddMediaWikiClient</c> registers it, so a
/// failure surfaces as an <see cref="OptionsValidationException"/>, and the
/// <see cref="MediaWikiClient(MediaWikiOptions, HttpMessageHandler?)"/> constructor calls it and throws an
/// <see cref="ArgumentException"/>.
/// </summary>
internal sealed class MediaWikiOptionsValidator : IValidateOptions<MediaWikiOptions>
{
    /// <summary>
    /// The end of the message refusing a base address that is not http or https, shared with the
    /// <see cref="MediaWikiClient"/> constructor so both paths word it alike.
    /// </summary>
    internal const string HttpSchemeRequirement = "must be an absolute http or https URL, e.g. \"https://en.wikipedia.org/w/rest.php/v1/\".";

    /// <summary>
    /// The end of the message refusing a base address with a query string or fragment, shared with the
    /// <see cref="MediaWikiClient"/> constructor so both paths word it alike.
    /// </summary>
    internal const string NoQueryOrFragmentRequirement =
        "must not have a query string or fragment, since every request drops both, e.g. \"https://en.wikipedia.org/w/rest.php/v1/\".";

    /// <summary>
    /// The end of the message refusing a base address whose path lacks a trailing slash, shared with the
    /// <see cref="MediaWikiClient"/> constructor so both paths word it alike.
    /// </summary>
    internal const string TrailingSlashRequirement =
        "must end with '/', or every request loses its last path segment, e.g. \"https://en.wikipedia.org/w/rest.php/v1/\".";

    /// <summary>
    /// The end of the message refusing a missing User-Agent, shared with the <see cref="MediaWikiClient"/> constructor so
    /// both paths word it alike.
    /// </summary>
    internal const string UserAgentRequirement = "must be provided as per the MediaWiki API guidelines.";

    /// <summary>The most <see cref="HttpClient.Timeout"/> accepts; a larger value throws when it is assigned.</summary>
    private static readonly TimeSpan MaxTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

    /// <summary>Checks every rule and reports each one broken, so a misconfigured wiki is fixed in one pass.</summary>
    /// <param name="name">The wiki the options belong to, named in each message unless it is the default one.</param>
    /// <param name="options">The options to check.</param>
    public ValidateOptionsResult Validate(string? name, MediaWikiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var wiki = string.IsNullOrEmpty(name) ? string.Empty : $" for wiki \"{name}\"";
        var failures = new List<string>();

        // The rules on the path only apply to a URL that parsed, or one bad value would be reported three times.
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme is not ("http" or "https"))
        {
            failures.Add($"{nameof(MediaWikiOptions)}.{nameof(MediaWikiOptions.BaseUrl)}{wiki} {HttpSchemeRequirement}");
        }

        if (baseUri is not null && !baseUri.AbsolutePath.EndsWith('/'))
        {
            failures.Add($"{nameof(MediaWikiOptions)}.{nameof(MediaWikiOptions.BaseUrl)}{wiki} {TrailingSlashRequirement}");
        }

        if (baseUri is not null && (baseUri.Query.Length > 0 || baseUri.Fragment.Length > 0))
        {
            failures.Add($"{nameof(MediaWikiOptions)}.{nameof(MediaWikiOptions.BaseUrl)}{wiki} {NoQueryOrFragmentRequirement}");
        }

        if ((options.Timeout <= TimeSpan.Zero || options.Timeout > MaxTimeout) && options.Timeout != Timeout.InfiniteTimeSpan)
        {
            failures.Add(
                $"{nameof(MediaWikiOptions)}.{nameof(MediaWikiOptions.Timeout)}{wiki} must be greater than zero and at most {MaxTimeout}, or Timeout.InfiniteTimeSpan. A configuration value is read as d.hh:mm:ss, so \"30\" is 30 days; write 30 seconds as \"00:00:30\".");
        }

        if (options.MaxResponseSize <= 0)
        {
            failures.Add($"{nameof(MediaWikiOptions)}.{nameof(MediaWikiOptions.MaxResponseSize)}{wiki} must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(options.UserAgent))
        {
            failures.Add($"{nameof(MediaWikiOptions)}.{nameof(MediaWikiOptions.UserAgent)}{wiki} {UserAgentRequirement}");
        }
        else if (!IsValidUserAgent(options.UserAgent))
        {
            failures.Add(
                $"{nameof(MediaWikiOptions)}.{nameof(MediaWikiOptions.UserAgent)}{wiki} is not a valid User-Agent header. Expected e.g. \"MyApp/1.0 (https://example.com; contact@example.com)\".");
        }

        if (!string.IsNullOrWhiteSpace(options.AccessToken) && options.AccessTokenProvider is not null)
        {
            failures.Add(
                $"{nameof(MediaWikiOptions)}.{nameof(MediaWikiOptions.AccessToken)} and {nameof(MediaWikiOptions.AccessTokenProvider)}{wiki} cannot both be set.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>Whether the header parser accepts the value, as it must when the value is added to the client's headers.</summary>
    private static bool IsValidUserAgent(string userAgent)
    {
        using var request = new HttpRequestMessage();

        return request.Headers.UserAgent.TryParseAdd(userAgent);
    }
}
