using Xunit;

namespace TimBearCity.MediaWiki.Tests;

public sealed class MediaWikiClientTests
{
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
