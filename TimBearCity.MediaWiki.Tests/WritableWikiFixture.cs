using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace TimBearCity.MediaWiki.Tests;

/// <summary>
/// The throwaway wiki the write smoke tests edit, configured from the environment and shared by every test in
/// <see cref="MediaWikiClientWriteSmokeTests"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately a separate opt-in from <see cref="ReadableWikiFixture"/>: pointing <c>MEDIAWIKI_SMOKE_READ_BASE_URL</c>
/// at a real wiki never makes anything write to it. <c>MEDIAWIKI_SMOKE_WRITE_BASE_URL</c> is meant for a wiki that is
/// created for the run and thrown away afterward, which is what the CI job does with the official Docker image.
/// </para>
/// <para>
/// With no <c>MEDIAWIKI_SMOKE_WRITE_ACCESS_TOKEN</c> the edits are anonymous, which a throwaway wiki allows, and the
/// CSRF token is MediaWiki's logged-out token <c>+\</c>. Set the access token to write as a user instead; the CSRF
/// token then goes unused, as bearer authentication is safe against CSRF.
/// </para>
/// </remarks>
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "A class fixture must be as visible as the test class that takes it.")]
// ReSharper disable once ClassNeverInstantiated.Global
public sealed class WritableWikiFixture : IDisposable
{
    /// <summary>The REST API root of the throwaway wiki, or <see langword="null"/> when the write smoke tests are not opted in.</summary>
    public static readonly string? BaseUrl = Environment.GetEnvironmentVariable("MEDIAWIKI_SMOKE_WRITE_BASE_URL");

    private static readonly string? AccessToken = Environment.GetEnvironmentVariable("MEDIAWIKI_SMOKE_WRITE_ACCESS_TOKEN");

    private readonly ServiceProvider _services;

    public WritableWikiFixture()
    {
        var services = new ServiceCollection();

        if (BaseUrl is not null)
        {
            services.AddMediaWikiClient(options =>
            {
                options.BaseUrl = BaseUrl;
                options.UserAgent = "TimBearCity.MediaWiki write smoke tests (https://github.com/timbearcity/MediaWiki)";
                options.AccessToken = AccessToken;
            });
        }

        _services = services.BuildServiceProvider();
    }

    public IMediaWikiClient Client => _services.GetRequiredService<IMediaWikiClient>();

    /// <summary>The CSRF token to send with an edit: MediaWiki's logged-out token for anonymous edits, nothing for bearer authentication.</summary>
    public string? CsrfToken { get; } = AccessToken is null ? "+\\" : null;

    public void Dispose()
    {
        _services.Dispose();
    }

    /// <summary>A page title no earlier run can have used, so every test starts from a page that does not exist.</summary>
    public static string BuildNewTitle()
    {
        return $"Smoke/{Guid.NewGuid():N}";
    }
}
