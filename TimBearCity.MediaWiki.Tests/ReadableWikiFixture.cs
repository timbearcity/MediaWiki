using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace TimBearCity.MediaWiki.Tests;

/// <summary>
/// The live wiki the smoke tests read from, configured from the environment and shared by every test in
/// <see cref="MediaWikiClientReadSmokeTests"/>.
/// </summary>
/// <remarks>
/// The client goes through <c>AddMediaWikiClient</c> rather than a bare <see cref="HttpClient"/>, so the registration
/// path is exercised against a real wiki too. Nothing is registered when <see cref="BaseUrl"/> is unset: the tests are
/// skipped before they ask for the client.
/// </remarks>
[SuppressMessage("Maintainability", "CA1515:Consider making public types internal",
    Justification = "A class fixture must be as visible as the test class that takes it.")]
// ReSharper disable once ClassNeverInstantiated.Global
public sealed class ReadableWikiFixture : IDisposable
{
    /// <summary>The REST API root under test, or <see langword="null"/> when the smoke tests are not opted in.</summary>
    public static readonly string? BaseUrl = Environment.GetEnvironmentVariable("MEDIAWIKI_SMOKE_READ_BASE_URL");

    private readonly ServiceProvider _services;

    public ReadableWikiFixture()
    {
        Page = Environment.GetEnvironmentVariable("MEDIAWIKI_SMOKE_READ_PAGE") ?? "Albert_Einstein";
        File = Environment.GetEnvironmentVariable("MEDIAWIKI_SMOKE_READ_FILE") ?? "File:Wiki.png";

        var services = new ServiceCollection();

        if (BaseUrl is not null)
        {
            services.AddMediaWikiClient(options =>
            {
                options.BaseUrl = BaseUrl;
                options.UserAgent = "TimBearCity.MediaWiki read smoke tests (https://github.com/timbearcity/MediaWiki)";
            });
        }

        _services = services.BuildServiceProvider();
    }

    public IMediaWikiClient Client => _services.GetRequiredService<IMediaWikiClient>();

    /// <summary>A file the wiki hosts itself, with the <c>File:</c> prefix.</summary>
    public string File { get; }

    /// <summary>The key of a page the wiki has, ideally one with a long history.</summary>
    public string Page { get; }

    public void Dispose()
    {
        _services.Dispose();
    }
}
