using Xunit;

namespace TimBearCity.MediaWiki.Tests;

public sealed class MediaWikiClientTests
{
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

        var client = new MediaWikiClient(httpClient);

        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_HttpClientWithBaseAddress_Succeeds()
    {
        using var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri("https://example.org/w/rest.php/", UriKind.Absolute);

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
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new MediaWikiClient(null!));
    }
}
