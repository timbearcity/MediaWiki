using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using System.Reflection;

namespace TimBearCity.MediaWiki;

/// <summary>
/// What both ways of building a client put on the <see cref="HttpClient"/> and its handler pipeline, so that a client
/// constructed from <see cref="MediaWikiOptions"/> behaves as one <c>AddMediaWikiClient</c> registered.
/// </summary>
internal static class MediaWikiPipeline
{
    private static readonly string LibraryUserAgent = BuildLibraryUserAgent();

    /// <summary>Adds the handlers that sit above the primary handler, outermost first.</summary>
    /// <param name="handlers">The pipeline's handlers, which these go after.</param>
    /// <param name="options">Validated options.</param>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The pipeline the handlers join owns them.")]
    internal static void AddHandlers(ICollection<DelegatingHandler> handlers, MediaWikiOptions options)
    {
        // Ahead of the token handler, so that the token is set on each hop of a redirect and not just the first request.
        handlers.Add(new RedirectHandler());

        // A wiki without a token gets no token handler at all.
        if (options.AccessTokenProvider is { } accessTokenProvider)
        {
            handlers.Add(new AccessTokenHandler(accessTokenProvider));
        }
        else if (options.AccessToken is { } accessToken && !string.IsNullOrWhiteSpace(accessToken))
        {
            handlers.Add(new AccessTokenHandler(_ => ValueTask.FromResult<string?>(accessToken)));
        }
    }

    /// <summary>Applies the base address, timeout, size cap and headers to the client.</summary>
    /// <param name="client">A client that has not sent a request yet.</param>
    /// <param name="options">Validated options, so the User-Agent is known to parse.</param>
    internal static void ConfigureHttpClient(HttpClient client, MediaWikiOptions options)
    {
        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = options.Timeout;

        if (options.MaxResponseSize is { } maxResponseSize)
        {
            client.MaxResponseContentBufferSize = maxResponseSize;
        }

        client.DefaultRequestHeaders.Accept.ParseAdd(MediaTypeNames.Application.Json);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(LibraryUserAgent);
    }

    /// <summary>
    /// Stops the primary handler from following redirects itself, leaving them to <see cref="RedirectHandler"/>. A handler
    /// of another type is left as it is: it has no <c>AllowAutoRedirect</c> to switch off.
    /// </summary>
    /// <exception cref="InvalidOperationException">The handler follows redirects and has already sent a request.</exception>
    internal static void DisableAutoRedirect(HttpMessageHandler primaryHandler)
    {
        // Only assigned when it changes anything, since a handler that has sent a request refuses any assignment.
        switch (primaryHandler)
        {
            case HttpClientHandler { AllowAutoRedirect: true } handler:
                handler.AllowAutoRedirect = false;
                break;
            case SocketsHttpHandler { AllowAutoRedirect: true } handler:
                handler.AllowAutoRedirect = false;
                break;
        }
    }

    private static string BuildLibraryUserAgent()
    {
        var assembly = typeof(MediaWikiClient).Assembly;
        var version = GetVersion(assembly);

        // Informational versions may carry source-control metadata ("1.0.0+abc1234"), which is not a valid header token.
        var plusIndex = version.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex >= 0)
        {
            version = version[..plusIndex];
        }

        return $"{assembly.GetName().Name}/{version}";
    }

    /// <summary>This assembly's version, for the library half of the User-Agent header.</summary>
    /// <remarks>
    /// The SDK stamps every build with an informational version, so only a hand-assembled build reaches the
    /// fallbacks; there is no way to hand this method such an assembly from a test, hence the exclusion.
    /// </remarks>
    [ExcludeFromCodeCoverage(Justification = "The fallbacks cover assemblies the SDK did not stamp, which no test can produce.")]
    private static string GetVersion(Assembly assembly)
    {
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? assembly.GetName().Version?.ToString()
               ?? "0.0.0";
    }
}
