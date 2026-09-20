using System.Net;
using System.Net.Mime;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace TimBearCity.MediaWiki.Tests;

public sealed class ServiceCollectionExtensionsTests : IDisposable
{
    private const string BaseUrl = "https://en.wikipedia.org/w/rest.php/v1/";
    private const string CommonsBaseUrl = "https://commons.wikimedia.org/w/rest.php/v1/";

    private const string ForbiddenJson =
        """
        {
          "errorKey": "rest-permission-denied-create",
          "messageTranslations": { "en": "You do not have permission to create this page." },
          "httpReason": "Forbidden"
        }
        """;

    private const string PagesJson = """{ "pages": [] }""";
    private const string UserAgent = "MyApp/1.0 (https://example.com; contact@example.com)";

    private readonly HttpMessageHandlerStub _commonsHandler = HttpMessageHandlerStub.CreateReturningJson(PagesJson);

    private readonly HttpMessageHandlerStub _forbiddenHandler = HttpMessageHandlerStub.CreateReturningJson(ForbiddenJson, HttpStatusCode.Forbidden);

    private readonly HttpMessageHandlerStub _htmlHandler =
        HttpMessageHandlerStub.CreateReturningContent("<!DOCTYPE html><p>Hello world</p>", MediaTypeNames.Text.Html);

    private readonly HttpClientHandler _httpClientHandler = new() { AllowAutoRedirect = true };

    private readonly SocketsHttpHandler _socketsHttpHandler = new() { AllowAutoRedirect = true };

    private readonly HttpMessageHandlerStub _wikipediaHandler = HttpMessageHandlerStub.CreateReturningJson(PagesJson);

    public void Dispose()
    {
        _commonsHandler.Dispose();
        _forbiddenHandler.Dispose();
        _htmlHandler.Dispose();
        _httpClientHandler.Dispose();
        _socketsHttpHandler.Dispose();
        _wikipediaHandler.Dispose();
    }

    [Fact]
    public async Task AddMediaWikiClient_AccessTokenAndProvider_ThrowsOptionsValidationException()
    {
        var services = new ServiceCollection();

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
            options.AccessToken = "secret-token";
            options.AccessTokenProvider = _ => ValueTask.FromResult<string?>("other-token");
        });

        await using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(provider.GetRequiredService<IMediaWikiClient>);

        Assert.Contains(nameof(MediaWikiOptions.AccessTokenProvider), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddMediaWikiClient_AccessTokenProvider_AsksPerRequest()
    {
        var tokens = new Queue<string?>(["first-token", "second-token"]);
        var services = new ServiceCollection();

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
            options.AccessTokenProvider = _ => ValueTask.FromResult(tokens.Dequeue());
        }).ConfigurePrimaryHttpMessageHandler(() => _wikipediaHandler);

        await using var provider = services.BuildServiceProvider();

        var client = provider.GetRequiredService<IMediaWikiClient>();
        await client.SearchPagesAsync("physicist", 10, TestContext.Current.CancellationToken);
        await client.SearchPagesAsync("physicist", 10, TestContext.Current.CancellationToken);

        Assert.Equal(["first-token", "second-token"], _wikipediaHandler.Requests.Select(request => request.Headers.Authorization?.Parameter));
        Assert.All(_wikipediaHandler.Requests, request => Assert.Equal("Bearer", request.Headers.Authorization?.Scheme));
    }

    [Fact]
    public async Task AddMediaWikiClient_AccessTokenProviderAnswersNull_OmitsAuthorizationHeader()
    {
        var services = new ServiceCollection();

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
            options.AccessTokenProvider = _ => ValueTask.FromResult<string?>(null);
        }).ConfigurePrimaryHttpMessageHandler(() => _wikipediaHandler);

        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IMediaWikiClient>().SearchPagesAsync("physicist", 10, TestContext.Current.CancellationToken);

        Assert.Null(_wikipediaHandler.Request.Headers.Authorization);
    }

    [Fact]
    public async Task AddMediaWikiClient_AccessTokenProviderOnNamedWiki_AppliesToThatWikiOnly()
    {
        var services = new ServiceCollection();

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
        }).ConfigurePrimaryHttpMessageHandler(() => _wikipediaHandler);

        services.AddMediaWikiClient("commons", options =>
        {
            options.BaseUrl = CommonsBaseUrl;
            options.UserAgent = UserAgent;
            options.AccessTokenProvider = _ => ValueTask.FromResult<string?>("commons-token");
        }).ConfigurePrimaryHttpMessageHandler(() => _commonsHandler);

        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IMediaWikiClient>().SearchPagesAsync("physicist", 10, TestContext.Current.CancellationToken);
        await provider.GetRequiredKeyedService<IMediaWikiClient>("commons").SearchPagesAsync("physicist", 10, TestContext.Current.CancellationToken);

        Assert.Null(_wikipediaHandler.Request.Headers.Authorization);
        Assert.Equal("commons-token", _commonsHandler.Request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task AddMediaWikiClient_AppliesTimeoutAndMaxResponseSizeToHttpClient()
    {
        var services = new ServiceCollection();

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
            options.Timeout = TimeSpan.FromSeconds(5);
            options.MaxResponseSize = 64 * 1024 * 1024;
        });

        await using var provider = services.BuildServiceProvider();

        using var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IMediaWikiClient));

        Assert.Equal(TimeSpan.FromSeconds(5), httpClient.Timeout);
        Assert.Equal(64 * 1024 * 1024, httpClient.MaxResponseContentBufferSize);
    }

    [Fact]
    public async Task AddMediaWikiClient_BaseUrlWithoutTrailingSlash_AppendsTrailingSlash()
    {
        var services = new ServiceCollection();

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = "https://en.wikipedia.org/w/rest.php/v1";
            options.UserAgent = UserAgent;
        }).ConfigurePrimaryHttpMessageHandler(() => _wikipediaHandler);

        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IMediaWikiClient>().SearchPagesAsync("physicist", 10, TestContext.Current.CancellationToken);

        Assert.Equal($"{BaseUrl}search/page?q=physicist&limit=10", _wikipediaHandler.Request.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task AddMediaWikiClient_ConfigurationSection_BindsOptions()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{MediaWikiOptions.Position}:BaseUrl"] = BaseUrl,
            [$"{MediaWikiOptions.Position}:UserAgent"] = UserAgent,
            [$"{MediaWikiOptions.Position}:AccessToken"] = "secret-token",
            [$"{MediaWikiOptions.Position}:Timeout"] = "00:00:15",
            [$"{MediaWikiOptions.Position}:MaxResponseSize"] = "1048576"
        });

        var services = new ServiceCollection();

        services.AddMediaWikiClient(configuration.GetSection(MediaWikiOptions.Position))
            .ConfigurePrimaryHttpMessageHandler(() => _wikipediaHandler);

        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<MediaWikiOptions>>().Get(Options.DefaultName);

        Assert.Equal(BaseUrl, options.BaseUrl);
        Assert.Equal(UserAgent, options.UserAgent);
        Assert.Equal("secret-token", options.AccessToken);
        Assert.Equal(TimeSpan.FromSeconds(15), options.Timeout);
        Assert.Equal(1_048_576, options.MaxResponseSize);
    }

    [Fact]
    public async Task AddMediaWikiClient_ConfiguresBaseAddressAndHeaders()
    {
        var services = new ServiceCollection();

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
            options.AccessToken = "secret-token";
        }).ConfigurePrimaryHttpMessageHandler(() => _wikipediaHandler);

        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IMediaWikiClient>().SearchPagesAsync("physicist", 10, TestContext.Current.CancellationToken);

        var request = _wikipediaHandler.Request;
        var userAgent = request.Headers.UserAgent.ToString();

        Assert.Equal($"{BaseUrl}search/page?q=physicist&limit=10", request.RequestUri?.AbsoluteUri);
        Assert.StartsWith(UserAgent, userAgent, StringComparison.Ordinal);
        Assert.Contains("MediaWiki/", userAgent, StringComparison.Ordinal);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("secret-token", request.Headers.Authorization?.Parameter);
        Assert.Contains(request.Headers.Accept, header => header.MediaType == MediaTypeNames.Application.Json);
    }

    [Theory]
    [InlineData(BaseUrl, "https://commons.wikimedia.org/w/rest.php/v1/page/Albert_Einstein")]
    [InlineData(BaseUrl, "https://en.wikipedia.org:8443/w/rest.php/v1/page/Albert_Einstein")]
    [InlineData("http://en.wikipedia.org/w/rest.php/v1/", "https://en.wikipedia.org/w/rest.php/v1/page/Albert_Einstein")]
    public async Task AddMediaWikiClient_CrossOriginRedirect_SendsTheHopAnonymously(string baseUrl, string location)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_ => HttpMessageHandlerStub.CreateRedirecting(HttpStatusCode.Moved, location, EinsteinPage.Json));

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = baseUrl;
            options.UserAgent = UserAgent;
            options.AccessToken = "secret-token";
        }).ConfigurePrimaryHttpMessageHandler<HttpMessageHandlerStub>();

        await using var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<HttpMessageHandlerStub>();

        await provider.GetRequiredService<IMediaWikiClient>().GetPageAsync(EinsteinPage.Key, TestContext.Current.CancellationToken);

        // The token was meant for the wiki, so a hop off it, even to the same host on another scheme or port, goes without.
        Assert.Equal(["secret-token", null], handler.Hops.Select(hop => hop.Token));
        Assert.Equal(location, handler.Hops[1].Uri);
    }

    [Fact]
    public async Task AddMediaWikiClient_CustomHttpClientHandler_TurnsOffItsAutoRedirect()
    {
        var services = new ServiceCollection();

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
        }).ConfigurePrimaryHttpMessageHandler(() => _httpClientHandler);

        await using var provider = services.BuildServiceProvider();

        // The primary handler would strip the token from every redirect it followed, whoever supplied it and in whatever order.
        Assert.Same(_httpClientHandler, GetPrimaryHandler(provider, nameof(IMediaWikiClient)));
        Assert.False(_httpClientHandler.AllowAutoRedirect);
    }

    [Fact]
    public async Task AddMediaWikiClient_CustomSocketsHttpHandler_TurnsOffItsAutoRedirect()
    {
        var services = new ServiceCollection();

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
        }).ConfigurePrimaryHttpMessageHandler(() => _socketsHttpHandler);

        await using var provider = services.BuildServiceProvider();

        Assert.Same(_socketsHttpHandler, GetPrimaryHandler(provider, nameof(IMediaWikiClient)));
        Assert.False(_socketsHttpHandler.AllowAutoRedirect);
    }

    [Fact]
    public async Task AddMediaWikiClient_DefaultPrimaryHandler_DoesNotFollowRedirects()
    {
        var services = new ServiceCollection();

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
        });

        await using var provider = services.BuildServiceProvider();

        // A typed client's HttpClient is named after the type it was registered for.
        Assert.False(GetAllowAutoRedirect(GetPrimaryHandler(provider, nameof(IMediaWikiClient))));
    }

    [Fact]
    public void AddMediaWikiClient_DefaultWikiRegisteredTwice_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
        });

        Assert.Throws<InvalidOperationException>(() => services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
        }));
    }

    [Fact]
    public async Task AddMediaWikiClient_FailedWrite_KeepsTokensOutOfTheException()
    {
        const string accessToken = "secret-access-token";
        const string csrfToken = @"9ed1499d99c0c34c73faa07157b3b6075b427365+\";
        var services = new ServiceCollection();

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
            options.AccessToken = accessToken;
        }).ConfigurePrimaryHttpMessageHandler(() => _forbiddenHandler);

        await using var provider = services.BuildServiceProvider();

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() => provider.GetRequiredService<IMediaWikiClient>()
            .CreatePageAsync("Wikipedia:Sandbox", "Hello, world.", "Testing the REST API", csrfToken: csrfToken,
                cancellationToken: TestContext.Current.CancellationToken));

        // Both tokens did go out, so their absence from the exception is the library's doing rather than a quiet request.
        Assert.Equal(accessToken, _forbiddenHandler.Request.Headers.Authorization?.Parameter);
        Assert.Contains(JsonEncodedText.Encode(csrfToken).ToString(), _forbiddenHandler.RequestBody, StringComparison.Ordinal);

        // ToString covers the message, the inner exceptions and the stack trace, which is everything a log would print.
        var printed = exception.ToString();

        Assert.DoesNotContain(accessToken, printed, StringComparison.Ordinal);
        Assert.DoesNotContain(csrfToken, printed, StringComparison.Ordinal);
        Assert.DoesNotContain("9ed1499d99c0c34c73faa07157b3b6075b427365", printed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddMediaWikiClient_HtmlEndpoint_AsksForHtmlInsteadOfTheJsonDefault()
    {
        var services = new ServiceCollection();
        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
        }).ConfigurePrimaryHttpMessageHandler(() => _htmlHandler);

        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IMediaWikiClient>()
            .TransformWikitextToHtmlAsync("== Hello world ==", cancellationToken: TestContext.Current.CancellationToken);

        // The wiki refuses this endpoint with 406 when JSON is the only acceptable type, so the default must not leak through.
        Assert.Equal(MediaTypeNames.Text.Html, Assert.Single(_htmlHandler.Request.Headers.Accept).MediaType);
    }

    [Theory]
    [InlineData("", UserAgent, 30, null)]
    [InlineData("not-a-url", UserAgent, 30, null)]
    [InlineData("localhost:8080/w/rest.php/v1/", UserAgent, 30, null)]
    [InlineData("ftp://en.wikipedia.org/w/rest.php/v1/", UserAgent, 30, null)]
    [InlineData(BaseUrl, "", 30, null)]
    [InlineData(BaseUrl, "   ", 30, null)]
    [InlineData(BaseUrl, UserAgent, 0, null)]
    [InlineData(BaseUrl, UserAgent, -1, null)]
    [InlineData(BaseUrl, UserAgent, int.MaxValue / 1000 + 1, null)]
    [InlineData(BaseUrl, UserAgent, 30, 0)]
    [InlineData(BaseUrl, UserAgent, 30, -1)]
    public async Task AddMediaWikiClient_InvalidOptions_ThrowsOptionsValidationException(
        string baseUrl,
        string userAgent,
        int timeoutSeconds,
        int? maxResponseSize)
    {
        var services = new ServiceCollection();

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = baseUrl;
            options.UserAgent = userAgent;
            options.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            options.MaxResponseSize = maxResponseSize;
        });

        await using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(provider.GetRequiredService<IMediaWikiClient>);
    }

    [Fact]
    public async Task AddMediaWikiClient_MalformedUserAgent_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = "MyApp/1.0 (unclosed comment";
        });

        await using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(provider.GetRequiredService<IMediaWikiClient>);

        Assert.Contains(nameof(MediaWikiOptions.UserAgent), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddMediaWikiClient_MaxResponseSizeUnset_LeavesHttpClientDefault()
    {
        var services = new ServiceCollection();

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
        });

        await using var provider = services.BuildServiceProvider();

        using var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IMediaWikiClient));
        using var defaultClient = new HttpClient();

        Assert.Equal(defaultClient.MaxResponseContentBufferSize, httpClient.MaxResponseContentBufferSize);
    }

    [Fact]
    public async Task AddMediaWikiClient_MultipleWikis_KeepsEachOnItsOwnBaseAddress()
    {
        var services = new ServiceCollection();

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
        }).ConfigurePrimaryHttpMessageHandler(() => _wikipediaHandler);

        services.AddMediaWikiClient("commons", options =>
        {
            options.BaseUrl = CommonsBaseUrl;
            options.UserAgent = UserAgent;
        }).ConfigurePrimaryHttpMessageHandler(() => _commonsHandler);

        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IMediaWikiClient>()
            .SearchPagesAsync("physicist", 10, TestContext.Current.CancellationToken);

        await provider.GetRequiredKeyedService<IMediaWikiClient>("commons")
            .SearchPagesAsync("physicist", 10, TestContext.Current.CancellationToken);

        Assert.StartsWith(BaseUrl, _wikipediaHandler.Request.RequestUri?.AbsoluteUri, StringComparison.Ordinal);
        Assert.StartsWith(CommonsBaseUrl, _commonsHandler.Request.RequestUri?.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMediaWikiClient_NamedWikiRegisteredTwice_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        services.AddMediaWikiClient("commons", options =>
        {
            options.BaseUrl = CommonsBaseUrl;
            options.UserAgent = UserAgent;
        });

        Assert.Throws<InvalidOperationException>(() => services.AddMediaWikiClient("commons", options =>
        {
            options.BaseUrl = CommonsBaseUrl;
            options.UserAgent = UserAgent;
        }));
    }

    [Fact]
    public async Task AddMediaWikiClient_NamedWikis_BindsASectionPerWiki()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{MediaWikiOptions.Position}:BaseUrl"] = BaseUrl,
            [$"{MediaWikiOptions.Position}:UserAgent"] = UserAgent,
            [$"{MediaWikiOptions.Position}:Commons:BaseUrl"] = CommonsBaseUrl,
            [$"{MediaWikiOptions.Position}:Commons:UserAgent"] = UserAgent
        });

        var services = new ServiceCollection();

        services.AddMediaWikiClient(configuration.GetSection(MediaWikiOptions.Position));
        services.AddMediaWikiClient("commons", configuration.GetSection($"{MediaWikiOptions.Position}:Commons"));

        await using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<MediaWikiOptions>>();

        Assert.Equal(BaseUrl, monitor.Get(Options.DefaultName).BaseUrl);
        Assert.Equal(CommonsBaseUrl, monitor.Get("commons").BaseUrl);
    }

    [Fact]
    public async Task AddMediaWikiClient_NoAccessToken_OmitsAuthorizationHeader()
    {
        var services = new ServiceCollection();

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
        }).ConfigurePrimaryHttpMessageHandler(() => _wikipediaHandler);

        await using var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<IMediaWikiClient>().SearchPagesAsync("physicist", 10, TestContext.Current.CancellationToken);

        Assert.Null(_wikipediaHandler.Request.Headers.Authorization);
    }

    [Fact]
    public async Task AddMediaWikiClient_OtherHttpClient_KeepsItsAutoRedirect()
    {
        var services = new ServiceCollection();

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
        });
        services.AddHttpClient("other");

        await using var provider = services.BuildServiceProvider();

        // The filter that turns redirects off sees every client the factory builds, and must leave the others alone.
        Assert.True(GetAllowAutoRedirect(GetPrimaryHandler(provider, "other")));
    }

    [Fact]
    public async Task AddMediaWikiClient_RedirectLoop_StopsAfterTenHops()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_ => HttpMessageHandlerStub.CreateRedirecting(HttpStatusCode.Moved, "page/Albert_Einstein", EinsteinPage.Json, true));

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
        }).ConfigurePrimaryHttpMessageHandler<HttpMessageHandlerStub>();

        await using var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<HttpMessageHandlerStub>();

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() =>
            provider.GetRequiredService<IMediaWikiClient>().GetPageAsync(EinsteinPage.Key, TestContext.Current.CancellationToken));

        // The request, then ten redirects, after which the last redirect is what the client gets to report.
        Assert.Equal(11, handler.Hops.Count);
        Assert.Equal(HttpStatusCode.Moved, exception.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("http://en.wikipedia.org/w/rest.php/v1/page/Albert_Einstein")]
    [InlineData("ftp://en.wikipedia.org/w/rest.php/v1/page/Albert_Einstein")]
    public async Task AddMediaWikiClient_RedirectNotToFollow_ReportsTheRedirect(string? location)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_ => HttpMessageHandlerStub.CreateRedirecting(HttpStatusCode.Moved, location, EinsteinPage.Json));

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
        }).ConfigurePrimaryHttpMessageHandler<HttpMessageHandlerStub>();

        await using var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<HttpMessageHandlerStub>();

        var exception = await Assert.ThrowsAsync<MediaWikiException>(() =>
            provider.GetRequiredService<IMediaWikiClient>().GetPageAsync(EinsteinPage.Key, TestContext.Current.CancellationToken));

        // No Location, a step-down from https to http, or a scheme HTTP cannot follow: the redirect is reported instead.
        Assert.Single(handler.Hops);
        Assert.Equal(HttpStatusCode.Moved, exception.StatusCode);
    }

    [Theory]
    [InlineData(BaseUrl, "https://en.wikipedia.org/w/rest.php/v1/page/Albert_Einstein?redirect=no")]
    [InlineData("http://en.wikipedia.org/w/rest.php/v1/", "http://en.wikipedia.org/w/rest.php/v1/page/Albert_Einstein?redirect=no")]
    public async Task AddMediaWikiClient_RedirectOnTheWiki_KeepsTheAccessToken(string baseUrl, string location)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_ => HttpMessageHandlerStub.CreateRedirecting(HttpStatusCode.TemporaryRedirect, location, EinsteinPage.Json));

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = baseUrl;
            options.UserAgent = UserAgent;
            options.AccessToken = "secret-token";
        }).ConfigurePrimaryHttpMessageHandler<HttpMessageHandlerStub>();

        await using var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<HttpMessageHandlerStub>();

        var page = await provider.GetRequiredService<IMediaWikiClient>().GetPageAsync(EinsteinPage.Key, TestContext.Current.CancellationToken);

        // HttpClientHandler would have sent the second hop anonymously, which a private wiki answers with 403.
        Assert.Equal([$"{baseUrl}page/{EinsteinPage.Key}", location], handler.Hops.Select(hop => hop.Uri));
        Assert.Equal(["secret-token", "secret-token"], handler.Hops.Select(hop => hop.Token));
        Assert.Equal(EinsteinPage.Title, page?.Title);
    }

    [Fact]
    public async Task AddMediaWikiClient_RedirectOnTheWikiWithProvider_AsksForTheTokenPerHop()
    {
        var tokens = new Queue<string?>(["first-token", "second-token"]);
        var services = new ServiceCollection();
        services.AddSingleton(_ => HttpMessageHandlerStub.CreateRedirecting(HttpStatusCode.Moved, "/w/rest.php/v1/page/Albert_Einstein", EinsteinPage.Json));

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
            options.AccessTokenProvider = _ => ValueTask.FromResult(tokens.Dequeue());
        }).ConfigurePrimaryHttpMessageHandler<HttpMessageHandlerStub>();

        await using var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<HttpMessageHandlerStub>();

        await provider.GetRequiredService<IMediaWikiClient>().GetPageAsync("Albert Einstein", TestContext.Current.CancellationToken);

        // A relative Location resolves against the hop it came from, as the primary handler would resolve it.
        Assert.Equal([$"{BaseUrl}page/Albert_Einstein", $"{BaseUrl}page/Albert_Einstein"], handler.Hops.Select(hop => hop.Uri));
        Assert.Equal(["first-token", "second-token"], handler.Hops.Select(hop => hop.Token));
    }

    [Theory]
    [InlineData(HttpStatusCode.Moved, "POST", "GET")]
    [InlineData(HttpStatusCode.Moved, "PUT", "PUT")]
    [InlineData(HttpStatusCode.Found, "POST", "GET")]
    [InlineData(HttpStatusCode.SeeOther, "GET", "GET")]
    [InlineData(HttpStatusCode.SeeOther, "PUT", "GET")]
    [InlineData(HttpStatusCode.TemporaryRedirect, "POST", "POST")]
    [InlineData(HttpStatusCode.PermanentRedirect, "PUT", "PUT")]
    public async Task AddMediaWikiClient_RedirectStatus_RewritesTheMethodAsTheProtocolSays(HttpStatusCode statusCode, string method, string expectedMethod)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_ => HttpMessageHandlerStub.CreateRedirecting(statusCode, $"{BaseUrl}page/Albert_Einstein", EinsteinPage.Json));

        services.AddMediaWikiClient(options =>
        {
            options.BaseUrl = BaseUrl;
            options.UserAgent = UserAgent;
        }).ConfigurePrimaryHttpMessageHandler<HttpMessageHandlerStub>();

        await using var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<HttpMessageHandlerStub>();
        var client = provider.GetRequiredService<IMediaWikiClient>();
        var cancellationToken = TestContext.Current.CancellationToken;

        switch (method)
        {
            case "GET":
                await client.GetPageAsync(EinsteinPage.Key, cancellationToken);
                break;
            case "POST":
                await client.CreatePageAsync(EinsteinPage.Title, EinsteinPage.Source, "Created", cancellationToken: cancellationToken);
                break;
            default:
                await client.UpdatePageAsync(EinsteinPage.Key, EinsteinPage.Source, "Updated", cancellationToken: cancellationToken);
                break;
        }

        // A 303, or a 301 or 302 to a POST, is re-sent as a GET without the body; a 307 or 308 repeats the request as it was.
        Assert.Equal([method, expectedMethod], handler.Hops.Select(hop => hop.Method.Method));
        Assert.Equal(expectedMethod == "GET" ? null : handler.RequestBodies[0], handler.RequestBodies[1]);
    }

    [Fact]
    public async Task AddMediaWikiClient_SectionOmitsTimeout_LeavesTimeoutAtDefault()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{MediaWikiOptions.Position}:BaseUrl"] = BaseUrl,
            [$"{MediaWikiOptions.Position}:UserAgent"] = UserAgent
        });

        var services = new ServiceCollection();

        services.AddMediaWikiClient(configuration.GetSection(MediaWikiOptions.Position));

        await using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<MediaWikiOptions>>().Get(Options.DefaultName);

        Assert.Equal(TimeSpan.FromSeconds(30), options.Timeout);
        Assert.Null(options.AccessToken);
        Assert.Null(options.MaxResponseSize);
    }

    [Fact]
    public async Task AddMediaWikiClient_SectionTimeoutInWholeDays_ThrowsOptionsValidationExceptionNamingTheFormat()
    {
        // "30" is 30 days to TimeSpan.Parse, above what HttpClient accepts, and the likely intent was 30 seconds.
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{MediaWikiOptions.Position}:BaseUrl"] = BaseUrl,
            [$"{MediaWikiOptions.Position}:UserAgent"] = UserAgent,
            [$"{MediaWikiOptions.Position}:Timeout"] = "30"
        });

        var services = new ServiceCollection();

        services.AddMediaWikiClient(configuration.GetSection(MediaWikiOptions.Position));

        await using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(provider.GetRequiredService<IMediaWikiClient>);

        Assert.Contains("d.hh:mm:ss", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(nameof(MediaWikiOptions.MaxResponseSize), "64MB")]
    [InlineData(nameof(MediaWikiOptions.MaxResponseSize), "2147483648")]
    [InlineData(nameof(MediaWikiOptions.Timeout), "half a minute")]
    public async Task AddMediaWikiClient_UnparsableSectionValue_ThrowsInvalidOperationException(string key, string value)
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            [$"{MediaWikiOptions.Position}:BaseUrl"] = BaseUrl,
            [$"{MediaWikiOptions.Position}:UserAgent"] = UserAgent,
            [$"{MediaWikiOptions.Position}:{key}"] = value
        });

        var services = new ServiceCollection();

        services.AddMediaWikiClient(configuration.GetSection(MediaWikiOptions.Position));

        await using var provider = services.BuildServiceProvider();
        var monitor = provider.GetRequiredService<IOptionsMonitor<MediaWikiOptions>>();

        var exception = Assert.Throws<InvalidOperationException>(() => monitor.Get(Options.DefaultName));

        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    /// <summary>Whether the factory's default primary handler, whichever of the two a runtime uses, follows redirects itself.</summary>
    private static bool GetAllowAutoRedirect(HttpMessageHandler primaryHandler)
    {
        return primaryHandler switch
        {
            HttpClientHandler handler => handler.AllowAutoRedirect,
            SocketsHttpHandler handler => handler.AllowAutoRedirect,
            _ => throw new ArgumentException($"Expected a runtime primary handler, but got {primaryHandler.GetType()}.", nameof(primaryHandler))
        };
    }

    /// <summary>The handler at the bottom of the pipeline the factory builds for the named client.</summary>
    private static HttpMessageHandler GetPrimaryHandler(IServiceProvider provider, string name)
    {
        var handler = provider.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(name);

        while (handler is DelegatingHandler { InnerHandler: { } innerHandler })
        {
            handler = innerHandler;
        }

        return handler;
    }
}
