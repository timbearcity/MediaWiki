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

    private readonly HttpMessageHandlerStub _wikipediaHandler = HttpMessageHandlerStub.CreateReturningJson(PagesJson);

    public void Dispose()
    {
        _commonsHandler.Dispose();
        _forbiddenHandler.Dispose();
        _htmlHandler.Dispose();
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
    [InlineData(BaseUrl, "", 30, null)]
    [InlineData(BaseUrl, "   ", 30, null)]
    [InlineData(BaseUrl, UserAgent, 0, null)]
    [InlineData(BaseUrl, UserAgent, -1, null)]
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
}
