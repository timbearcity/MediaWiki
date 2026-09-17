using System.Net;

namespace TimBearCity.MediaWiki;

/// <summary>
/// Follows redirects in place of the primary handler, so that the handlers inside it see every hop and can
/// re-apply what the wiki expects on each, above all the bearer token.
/// </summary>
/// <remarks>
/// <see cref="HttpClientHandler"/> strips <c>Authorization</c> from every redirected request, even one that stays
/// on the wiki, and MediaWiki redirects freely: a page endpoint answers a title that needs normalizing with a
/// <c>301</c>, and the HTML and lint endpoints answer a redirect page with a <c>307</c> to its target. The rules
/// mirror the primary handler's: a <c>303</c> re-sends as <c>GET</c>, as do a <c>301</c> and <c>302</c> to a
/// <c>POST</c>; a <c>307</c> and <c>308</c> keep the method and body; a hop off <c>https</c> onto <c>http</c> is not
/// taken; and after <see cref="MaxRedirects"/> hops the last redirect is returned as it is. A hop to another origin
/// goes out anonymously, since the token was meant for the wiki and not for wherever it points.
/// </remarks>
internal sealed class RedirectHandler : DelegatingHandler
{
    /// <summary>The most hops one request follows; a wiki needs one, so this only guards against a loop.</summary>
    private const int MaxRedirects = 10;

    /// <summary>
    /// Present on a request that must go out without the bearer token, which <see cref="AccessTokenHandler"/> honors.
    /// </summary>
    internal static readonly HttpRequestOptionsKey<bool> IsAnonymous = new("TimBearCity.MediaWiki.IsAnonymous");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        for (var redirects = 0; redirects < MaxRedirects; redirects++)
        {
            // Never null inside the pipeline: HttpClient has combined it with the base address, or thrown, before any handler runs.
            var requestUri = request.RequestUri!;

            if (GetRedirectUri(requestUri, response) is not { } redirectUri)
            {
                return response;
            }

            if (!IsSameOrigin(requestUri, redirectUri))
            {
                request.Options.Set(IsAnonymous, true);
            }

            if (ShouldSendAsGet(response.StatusCode, request.Method))
            {
                request.Method = HttpMethod.Get;
                request.Content = null;
            }

            request.RequestUri = redirectUri;

            response.Dispose();
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    /// <summary>The absolute URI a redirect response points at, or <see langword="null"/> if it is not one to follow.</summary>
    private static Uri? GetRedirectUri(Uri requestUri, HttpResponseMessage response)
    {
        if (response.StatusCode is not (HttpStatusCode.Moved or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect))
        {
            return null;
        }

        if (response.Headers.Location is not { } location)
        {
            return null;
        }

        var redirectUri = location.IsAbsoluteUri ? location : new Uri(requestUri, location);

        if (redirectUri.Scheme != Uri.UriSchemeHttp && redirectUri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        // Leaving https for http would expose whatever the request carries, so the primary handler refuses it too.
        return requestUri.Scheme == Uri.UriSchemeHttps && redirectUri.Scheme == Uri.UriSchemeHttp ? null : redirectUri;
    }

    private static bool IsSameOrigin(Uri requestUri, Uri redirectUri)
    {
        return Uri.Compare(requestUri, redirectUri, UriComponents.SchemeAndServer, UriFormat.UriEscaped, StringComparison.OrdinalIgnoreCase) == 0;
    }

    /// <summary>Whether the redirect turns the request into a bodiless <c>GET</c>, as browsers do.</summary>
    private static bool ShouldSendAsGet(HttpStatusCode statusCode, HttpMethod method)
    {
        return statusCode switch
        {
            HttpStatusCode.Moved or HttpStatusCode.Found => method == HttpMethod.Post,
            HttpStatusCode.SeeOther => method != HttpMethod.Get,
            _ => false
        };
    }
}
