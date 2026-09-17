using Microsoft.Extensions.Http;

namespace TimBearCity.MediaWiki;

/// <summary>
/// Stops the primary handler of each MediaWiki client from following redirects itself, leaving them to
/// <see cref="RedirectHandler"/>.
/// </summary>
/// <remarks>
/// A filter runs around every configuration the builder was given, including a <c>ConfigurePrimaryHttpMessageHandler</c>
/// the caller adds after <c>AddMediaWikiClient</c> returns, so it sees the primary handler that will actually be
/// used, whichever order the registrations came in. Filters apply to every named client, hence the check for
/// <see cref="RedirectHandler"/>, which only a MediaWiki client carries. A primary handler of another type is left
/// as it is: it has no <c>AllowAutoRedirect</c> to switch off, and it is the caller's to keep from redirecting.
/// </remarks>
internal sealed class RedirectHandlerBuilderFilter : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        return builder =>
        {
            next(builder);

            if (!builder.AdditionalHandlers.Any(handler => handler is RedirectHandler))
            {
                return;
            }

            switch (builder.PrimaryHandler)
            {
                case HttpClientHandler handler:
                    handler.AllowAutoRedirect = false;
                    break;
                case SocketsHttpHandler handler:
                    handler.AllowAutoRedirect = false;
                    break;
            }
        };
    }
}
