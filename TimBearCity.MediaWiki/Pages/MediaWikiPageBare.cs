using System.Text.Json.Serialization;
using TimBearCity.MediaWiki.Common;

namespace TimBearCity.MediaWiki.Pages;

/// <summary>A wiki page's metadata, without any of its content.</summary>
/// <param name="Id">The page identifier.</param>
/// <param name="Key">The page title in URL-friendly format.</param>
/// <param name="Title">The page title in reading-friendly format.</param>
/// <param name="Latest">Information about the latest revision.</param>
/// <param name="ContentModel">The type of content on the page, e.g. "wikitext".</param>
/// <param name="License">The license the page content is available under.</param>
/// <param name="HtmlUrl">The absolute URL the page's rendered HTML can be fetched from.</param>
/// <param name="RedirectTarget">The API path of the page this one redirects to, if it is a redirect.</param>
public sealed record MediaWikiPageBare(
    long Id,
    string Key,
    string Title,
    MediaWikiRevisionReference Latest,
    string ContentModel,
    MediaWikiLicense License,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("redirect_target")] string? RedirectTarget = null)
    : MediaWikiPageMetadata(Id, Key, Title, Latest, ContentModel, License);
