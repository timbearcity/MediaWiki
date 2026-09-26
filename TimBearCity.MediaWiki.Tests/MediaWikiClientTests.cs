using System.Net;
using System.Net.Mime;
using System.Net.Sockets;
using Xunit;

namespace TimBearCity.MediaWiki.Tests;

public sealed class MediaWikiClientTests
{
    private const string UserAgent = HttpMessageHandlerStub.UserAgent;

    [Fact]
    public void Constructor_BaseAddressNotHttp_ThrowsArgumentExceptionNamingTheScheme()
    {
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri("ftp://example.org/w/rest.php/v1/", UriKind.Absolute);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

        var exception = Assert.Throws<ArgumentException>(() => new MediaWikiClient(httpClient));

        Assert.Equal("httpClient", exception.ParamName);
        Assert.StartsWith(
            "HttpClient.BaseAddress must be an absolute http or https URL, e.g. \"https://en.wikipedia.org/w/rest.php/v1/\".",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_BaseAddressWithoutTrailingSlash_ThrowsArgumentExceptionNamingTheSlash()
    {
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri("https://example.org/w/rest.php/v1", UriKind.Absolute);

        var exception = Assert.Throws<ArgumentException>(() => new MediaWikiClient(httpClient));

        Assert.Equal("httpClient", exception.ParamName);
        Assert.StartsWith(
            "HttpClient.BaseAddress must end with '/', or every request loses its last path segment, e.g. \"https://en.wikipedia.org/w/rest.php/v1/\".",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://example.org/w/rest.php/v1/?apikey=abc")]
    [InlineData("https://example.org/w/rest.php/v1/#section")]
    public void Constructor_BaseAddressWithQueryOrFragment_ThrowsArgumentExceptionNamingThem(string baseAddress)
    {
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(baseAddress, UriKind.Absolute);

        var exception = Assert.Throws<ArgumentException>(() => new MediaWikiClient(httpClient));

        Assert.Equal("httpClient", exception.ParamName);
        Assert.StartsWith(
            "HttpClient.BaseAddress must not have a query string or fragment, since every request drops both, e.g. \"https://en.wikipedia.org/w/rest.php/v1/\".",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_HostOnlyBaseAddress_Succeeds()
    {
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri("https://example.org", UriKind.Absolute);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

        var client = new MediaWikiClient(httpClient);

        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_HttpClientWithBaseAddress_Succeeds()
    {
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri("https://example.org/w/rest.php/", UriKind.Absolute);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

        var client = new MediaWikiClient(httpClient);

        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_HttpClientWithoutBaseAddress_ThrowsArgumentException()
    {
        using var httpClient = new HttpClient();

        var exception = Assert.Throws<ArgumentException>(() => new MediaWikiClient(httpClient));

        Assert.Equal("httpClient", exception.ParamName);
    }

    [Fact]
    public void Constructor_HttpClientWithoutUserAgent_ThrowsArgumentExceptionNamingIt()
    {
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri("https://example.org/w/rest.php/v1/", UriKind.Absolute);

        var exception = Assert.Throws<ArgumentException>(() => new MediaWikiClient(httpClient));

        // HttpClient sends none by default, and Wikimedia wikis refuse such a request with 403 on the first call.
        Assert.Equal("httpClient", exception.ParamName);
        Assert.StartsWith(
            "HttpClient.DefaultRequestHeaders.UserAgent must be provided as per the MediaWiki API guidelines.",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_HttpClientWithUnvalidatedUserAgent_Succeeds()
    {
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri("https://example.org/w/rest.php/v1/", UriKind.Absolute);
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MyApp/1.0 (unclosed comment");

        var client = new MediaWikiClient(httpClient);

        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_InvalidOptions_ThrowsArgumentExceptionListingEachFailure()
    {
        var options = new MediaWikiOptions
        {
            BaseUrl = "https://example.org/w/rest.php/v1",
            UserAgent = "MyApp/1.0 (unclosed comment"
        };

        var exception = Assert.Throws<ArgumentException>(() => new MediaWikiClient(options));

        Assert.Equal("options", exception.ParamName);
        Assert.Contains("MediaWikiOptions.BaseUrl must end with '/'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("MediaWikiOptions.UserAgent is not a valid User-Agent header.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new MediaWikiClient(null!));

        // A bare null binds to this constructor, since the other would need its optional parameter filled in.
        Assert.Equal("httpClient", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new MediaWikiClient((MediaWikiOptions)null!));

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public async Task Constructor_Options_ConfiguresBaseAddressAndHeaders()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson("""{ "pages": [] }""");
        var client = new MediaWikiClient(CreateOptions("secret-token"), handler);

        await client.SearchPagesAsync("physicist", 10, TestContext.Current.CancellationToken);

        var request = handler.Request;
        var userAgent = request.Headers.UserAgent.ToString();

        Assert.Equal($"{HttpMessageHandlerStub.DefaultBaseAddress}search/page?q=physicist&limit=10", request.RequestUri?.AbsoluteUri);
        Assert.StartsWith(UserAgent, userAgent, StringComparison.Ordinal);
        Assert.Contains("MediaWiki/", userAgent, StringComparison.Ordinal);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("secret-token", request.Headers.Authorization?.Parameter);
        Assert.Contains(request.Headers.Accept, header => header.MediaType == MediaTypeNames.Application.Json);
    }

    [Fact]
    public async Task Constructor_OptionsWithAccessToken_KeepsItOnRedirectsOnTheWiki()
    {
        const string location = $"{HttpMessageHandlerStub.DefaultBaseAddress}page/Albert_Einstein";
        using var handler = HttpMessageHandlerStub.CreateRedirecting(HttpStatusCode.Moved, location, EinsteinPage.Json);
        var client = new MediaWikiClient(CreateOptions("secret-token"), handler);

        var page = await client.GetPageAsync("albert_Einstein", TestContext.Current.CancellationToken);

        // Over a plain HttpClient, the primary handler would have sent the second hop anonymously.
        Assert.Equal([$"{HttpMessageHandlerStub.DefaultBaseAddress}page/albert_Einstein", location], handler.Hops.Select(hop => hop.Uri));
        Assert.Equal(["secret-token", "secret-token"], handler.Hops.Select(hop => hop.Token));
        Assert.Equal(EinsteinPage.Title, page?.Title);
    }

    [Fact]
    public void Constructor_OptionsWithCustomHttpClientHandler_TurnsOffItsAutoRedirect()
    {
        using var handler = new HttpClientHandler();
        handler.AllowAutoRedirect = true;

        _ = new MediaWikiClient(CreateOptions(), handler);

        Assert.False(handler.AllowAutoRedirect);
    }

    [Fact]
    public void Constructor_OptionsWithCustomSocketsHttpHandler_TurnsOffItsAutoRedirect()
    {
        using var handler = new SocketsHttpHandler();
        handler.AllowAutoRedirect = true;

        _ = new MediaWikiClient(CreateOptions(), handler);

        Assert.False(handler.AllowAutoRedirect);
    }

    [Fact]
    public async Task Constructor_OptionsWithMaxResponseSize_RefusesALargerResponse()
    {
        using var handler = HttpMessageHandlerStub.CreateReturningJson(EinsteinPage.Json);
        var options = CreateOptions();
        options.MaxResponseSize = 16;
        var client = new MediaWikiClient(options, handler);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        var inner = Assert.IsType<HttpRequestException>(exception.InnerException);
        Assert.Equal(HttpRequestError.ConfigurationLimitExceeded, inner.HttpRequestError);
    }

    [Fact]
    public void Constructor_OptionsWithoutPrimaryHandler_Succeeds()
    {
        var client = new MediaWikiClient(CreateOptions());

        Assert.NotNull(client);
    }

    [Fact]
    public async Task Constructor_OptionsWithStartedHandlerFollowingRedirects_ThrowsInvalidOperationException()
    {
        using var handler = new SocketsHttpHandler();
        await StartAsync(handler);

        // It would strip the token from each redirect it followed, and it can no longer be told not to.
        Assert.Throws<InvalidOperationException>(() => new MediaWikiClient(CreateOptions(), handler));
    }

    [Theory]
    [InlineData(nameof(HttpClientHandler))]
    [InlineData(nameof(SocketsHttpHandler))]
    public async Task Constructor_OptionsWithStartedHandlerNotFollowingRedirects_Succeeds(string handlerType)
    {
        using HttpMessageHandler handler = handlerType == nameof(HttpClientHandler)
            ? new HttpClientHandler { AllowAutoRedirect = false }
            : new SocketsHttpHandler { AllowAutoRedirect = false };
        await StartAsync(handler);

        var client = new MediaWikiClient(CreateOptions(), handler);

        Assert.NotNull(client);
    }

    [Fact]
    public async Task Constructor_OptionsWithTimeout_AppliesItToTheRequest()
    {
        using var handler = HttpMessageHandlerStub.CreateBlocking();
        var options = CreateOptions();
        options.Timeout = TimeSpan.FromMilliseconds(50);
        var client = new MediaWikiClient(options, handler);

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => client.GetPageAsync("Albert_Einstein", TestContext.Current.CancellationToken));

        Assert.Contains("timed out after 00:00:00.0500000", exception.Message, StringComparison.Ordinal);
    }

    private static MediaWikiOptions CreateOptions(string? accessToken = null)
    {
        return new MediaWikiOptions
        {
            BaseUrl = HttpMessageHandlerStub.DefaultBaseAddress,
            UserAgent = UserAgent,
            AccessToken = accessToken
        };
    }

    /// <summary>
    /// Sends a request to a local port that never answers and gives up on it, which is enough for the handler to refuse any
    /// further change to its settings.
    /// </summary>
    private static async Task StartAsync(HttpMessageHandler handler)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        using var invoker = new HttpMessageInvoker(handler, false);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/"));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invoker.SendAsync(request, cancellation.Token)).ConfigureAwait(false);
    }
}
