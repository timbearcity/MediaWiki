using System.Diagnostics;
using System.Net;
using System.Net.Mime;
using System.Text;

namespace TimBearCity.MediaWiki.Tests;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that answers with a canned response and records what it was asked for.
/// </summary>
/// <remarks>
/// The library's only seam is the <see cref="HttpClient"/> it is handed, so this handler stands in for the wiki:
/// build one with a factory method, hand it to <see cref="CreateClient"/> or to
/// <c>ConfigurePrimaryHttpMessageHandler</c>, then assert over <see cref="Requests"/>.
/// </remarks>
internal sealed class HttpMessageHandlerStub : HttpMessageHandler
{
    /// <summary>The base address used when a test does not care which wiki it is talking to.</summary>
    public const string DefaultBaseAddress = "https://wiki.example/w/rest.php/v1/";

    private readonly List<Hop> _hops = [];
    private readonly List<string?> _requestBodies = [];
    private readonly List<HttpRequestMessage> _requests = [];
#if NET9_0_OR_GREATER
    private readonly Lock _requestsLock = new();
#else
    private readonly object _requestsLock = new();
#endif
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

    private HttpMessageHandlerStub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    /// <summary>
    /// Each request as it arrived, oldest first, including every hop of a redirect: <see cref="Requests"/> shows a
    /// re-sent message only in the state its last hop left it.
    /// </summary>
    public IReadOnlyList<Hop> Hops
    {
        get
        {
            lock (_requestsLock)
            {
                return [.. _hops];
            }
        }
    }

    /// <summary>The one request this handler received.</summary>
    /// <exception cref="InvalidOperationException">It received none, or more than one.</exception>
    public HttpRequestMessage Request
    {
        get
        {
            var requests = Requests;

            return requests.Count == 1
                ? requests[0]
                : throw new InvalidOperationException($"Expected exactly one request, but the handler received {requests.Count}.");
        }
    }

    /// <summary>The body of the one request this handler received, or <see langword="null"/> if it carried none.</summary>
    /// <remarks>
    /// Read as the request is sent, since the client disposes the request message, and its content with it,
    /// before the assertion runs.
    /// </remarks>
    /// <exception cref="InvalidOperationException">It received none, or more than one.</exception>
    public string? RequestBody
    {
        get
        {
            var requestBodies = RequestBodies;

            return requestBodies.Count == 1
                ? requestBodies[0]
                : throw new InvalidOperationException($"Expected exactly one request, but the handler received {requestBodies.Count}.");
        }
    }

    /// <summary>The bodies of the requests this handler received, oldest first, <see langword="null"/> for each that carried none.</summary>
    public IReadOnlyList<string?> RequestBodies
    {
        get
        {
            lock (_requestsLock)
            {
                return [.. _requestBodies];
            }
        }
    }

    /// <summary>The requests this handler received, oldest first.</summary>
    /// <remarks>
    /// A redirect is followed by re-sending the same message, so it appears here once per hop, in the state the last
    /// hop left it; <see cref="Hops"/> has each hop as it went out.
    /// </remarks>
    public IReadOnlyList<HttpRequestMessage> Requests
    {
        get
        {
            lock (_requestsLock)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Never answers, leaving the outcome to the client's timeout or the caller's cancellation token.</summary>
    public static HttpMessageHandlerStub CreateBlocking()
    {
        return new HttpMessageHandlerStub(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);

            throw new UnreachableException();
        });
    }

    /// <summary>
    /// Answers the first request, or every request if <paramref name="always"/>, with a redirect to
    /// <paramref name="location"/>, and the rest with <paramref name="json"/>.
    /// </summary>
    /// <param name="statusCode">The redirect status, e.g. <see cref="HttpStatusCode.Moved"/>.</param>
    /// <param name="location">The <c>Location</c> header, absolute or relative, or <see langword="null"/> to send none.</param>
    /// <param name="json">The body answered once the redirect has been followed.</param>
    /// <param name="always">Whether to redirect every request, which the client must refuse to follow forever.</param>
    public static HttpMessageHandlerStub CreateRedirecting(HttpStatusCode statusCode, string? location, string json, bool always = false)
    {
        ArgumentNullException.ThrowIfNull(json);

        var first = true;

        return CreateResponding(_ =>
        {
            if (!first && !always)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json) };
            }

            first = false;

            var response = new HttpResponseMessage(statusCode);

            if (location is not null)
            {
                response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
            }

            return response;
        });
    }

    /// <summary>Answers every request with <paramref name="content"/> under the given content type.</summary>
    public static HttpMessageHandlerStub CreateReturningContent(string content, string mediaType, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return CreateResponding(_ => new HttpResponseMessage(statusCode) { Content = new StringContent(content, Encoding.UTF8, mediaType) });
    }

    /// <summary>Answers every request with <paramref name="json"/>.</summary>
    public static HttpMessageHandlerStub CreateReturningJson(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return CreateResponding(_ => new HttpResponseMessage(statusCode) { Content = new StringContent(json, Encoding.UTF8, MediaTypeNames.Application.Json) });
    }

    /// <summary>Answers every request with <c>404</c> and the error body MediaWiki sends under <paramref name="errorKey"/>.</summary>
    /// <param name="errorKey">The key, e.g. <c>rest-nonexistent-title</c>; MediaWiki's own wording is not part of what the client reads.</param>
    public static HttpMessageHandlerStub CreateReturningNotFound(string errorKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorKey);

        var json = $$"""
                     {
                       "errorKey": "{{errorKey}}",
                       "messageTranslations": { "en": "The requested resource was not found ({{errorKey}})." },
                       "httpCode": 404,
                       "httpReason": "Not Found"
                     }
                     """;

        return CreateReturningJson(json, HttpStatusCode.NotFound);
    }

    /// <summary>Answers each request with whatever <paramref name="respond"/> builds for it.</summary>
    public static HttpMessageHandlerStub CreateResponding(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        ArgumentNullException.ThrowIfNull(respond);

        return new HttpMessageHandlerStub((request, _) => Task.FromResult(respond(request)));
    }

    /// <summary>Answers every request with <paramref name="statusCode"/> and no message body.</summary>
    public static HttpMessageHandlerStub CreateReturningStatus(HttpStatusCode statusCode)
    {
        return CreateResponding(_ => new HttpResponseMessage(statusCode));
    }

    /// <summary>Fails every request with <paramref name="exception"/>, standing in for a transport failure.</summary>
    public static HttpMessageHandlerStub CreateThrowing(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new HttpMessageHandlerStub((_, _) => Task.FromException<HttpResponseMessage>(exception));
    }

    /// <summary>An <see cref="HttpClient"/> over this handler, with the base address <see cref="MediaWikiClient"/> requires.</summary>
    /// <param name="baseAddress">The wiki's REST endpoint.</param>
    /// <param name="timeout">The client timeout; short by default, so a blocking handler does not stall the suite.</param>
    public HttpClient CreateClient(string baseAddress = DefaultBaseAddress, TimeSpan? timeout = null)
    {
        return new HttpClient(this, false)
        {
            BaseAddress = new Uri(baseAddress, UriKind.Absolute),
            Timeout = timeout ?? TimeSpan.FromSeconds(30)
        };
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        lock (_requestsLock)
        {
            _hops.Add(new Hop(request.Method, request.RequestUri!.AbsoluteUri, request.Headers.Authorization?.Parameter));
            _requestBodies.Add(body);
            _requests.Add(request);
        }

        return await _respond(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>One request as it went out: the method, the absolute URI and the bearer token, if any.</summary>
    internal sealed record Hop(HttpMethod Method, string Uri, string? Token);
}
