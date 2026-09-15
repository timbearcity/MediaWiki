using System.Net.Http.Headers;

namespace TimBearCity.MediaWiki;

/// <summary>Sets the bearer token on each request from <see cref="MediaWikiOptions.AccessTokenProvider"/>.</summary>
internal sealed class AccessTokenHandler(Func<CancellationToken, ValueTask<string?>> accessTokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = await accessTokenProvider(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
