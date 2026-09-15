namespace TimBearCity.MediaWiki.Tests;

/// <summary>
/// The page the read and edit tests share: the values a test reads back, and the JSON the stub answers with, built from them.
/// </summary>
internal static class EinsteinPage
{
    public const string ContentModel = "wikitext";
    public const string Html = "<!DOCTYPE html><p>Albert Einstein was a physicist.</p>";
    public const string HtmlUrl = "https://en.wikipedia.org/w/rest.php/v1/page/Albert_Einstein/html";
    public const long Id = 9228;
    public const string Key = "Albert_Einstein";
    public const long LatestRevisionId = 1234567;
    public const string LicenseTitle = "CC BY-SA 4.0";
    public const string Source = "Albert Einstein was a physicist.";
    public const string Title = "Albert Einstein";

    /// <summary>The page as the <c>page/{key}/bare</c> endpoint returns it: metadata and a link to the HTML.</summary>
    public static readonly string BareJson = Build("html_url", HtmlUrl);

    /// <summary>The page as the <c>page/{key}</c> endpoint returns it, source included.</summary>
    public static readonly string Json = Build("source", Source);

    /// <summary>The page as the <c>page/{key}/with_html</c> endpoint returns it: metadata and the rendered HTML.</summary>
    public static readonly string WithHtmlJson = Build("html", Html);

    /// <summary>The metadata every page endpoint returns, followed by the one string property that distinguishes the endpoint.</summary>
    private static string Build(string property, string value)
    {
        return FormattableString.Invariant($$"""
                                             {
                                               "id": {{Id}},
                                               "key": "{{Key}}",
                                               "title": "{{Title}}",
                                               "latest": { "id": {{LatestRevisionId}}, "timestamp": "2024-01-02T03:04:05Z" },
                                               "content_model": "{{ContentModel}}",
                                               "license": { "title": "{{LicenseTitle}}", "url": "https://creativecommons.org/licenses/by-sa/4.0/" },
                                               "{{property}}": "{{value}}"
                                             }
                                             """);
    }
}
