using System.Net.Http.Headers;

namespace TimBearCity.MediaWiki;

/// <summary>
/// Sets the bearer token on each request from <see cref="MediaWikiOptions.AccessTokenProvider"/>, and clears it when
/// the provider answers <see langword="null"/> or whitespace. A static <see cref="MediaWikiOptions.AccessToken"/> does
/// not come through here; <see cref="MediaWikiPipeline.ConfigureHttpClient"/> sets it on the <see cref="HttpClient"/>.
/// An answer that cannot be sent in a header throws <see cref="InvalidOperationException"/> naming the provider, as
/// <see cref="MediaWikiOptionsValidator"/> refuses such an <see cref="MediaWikiOptions.AccessToken"/>, rather than leaving
/// the header to throw <see cref="FormatException"/>, or the primary handler a transient failure.
/// </summary>
/// <remarks>
/// <para>
/// Sits inside <see cref="RedirectHandler"/>, so a redirect the wiki answers with goes out with the token as well,
/// unless the hop leaves the wiki and the request is marked <see cref="RedirectHandler.IsAnonymous"/>; the redirect
/// handler has cleared the header by then, and this one leaves it cleared.
/// </para>
/// <para>
/// The header is assigned whatever the provider answers, since a request can arrive carrying the token an earlier send
/// went out with: a redirect hop is a copy of the hop before it, and a retry by a handler outside this one re-sends the
/// same message. An <c>Authorization</c> header the caller set on the <see cref="HttpClient"/> is replaced the same way,
/// so the provider's answer is what every request goes out with.
/// </para>
/// </remarks>
internal sealed class AccessTokenHandler(Func<CancellationToken, ValueTask<string?>> accessTokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Options.TryGetValue(RedirectHandler.IsAnonymous, out _))
        {
            var accessToken = await accessTokenProvider(cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(accessToken) && !MediaWikiOptionsValidator.IsSendableAccessToken(accessToken))
            {
                throw new InvalidOperationException(
                    $"The token from {nameof(MediaWikiOptions)}.{nameof(MediaWikiOptions.AccessTokenProvider)} {MediaWikiOptionsValidator.AccessTokenRequirement}");
            }

            request.Headers.Authorization = string.IsNullOrWhiteSpace(accessToken) ? null : new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
