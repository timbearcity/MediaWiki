using System.Text.Json.Serialization;
using TimBearCity.MediaWiki.Common;

namespace TimBearCity.MediaWiki.Pages;

/// <summary>A wiki page, including its rendered HTML.</summary>
/// <remarks>
/// The endpoint answers a redirect page with a redirect of its own, so this representation is always the
/// target's, and carries no redirect target of its own.
/// </remarks>
/// <param name="Id">The page identifier.</param>
/// <param name="Key">The page title in URL-friendly format.</param>
/// <param name="Title">The page title in reading-friendly format.</param>
/// <param name="Latest">Information about the latest revision.</param>
/// <param name="ContentModel">The type of content on the page, e.g. "wikitext".</param>
/// <param name="License">The license the page content is available under.</param>
/// <param name="Html">The page content rendered as HTML.</param>
public sealed record MediaWikiPageWithHtml(
    long Id,
    string Key,
    string Title,
    MediaWikiRevisionReference Latest,
    string ContentModel,
    MediaWikiLicense License,
    [property: JsonPropertyName("html")] string Html)
    : MediaWikiPageMetadata(Id, Key, Title, Latest, ContentModel, License);
