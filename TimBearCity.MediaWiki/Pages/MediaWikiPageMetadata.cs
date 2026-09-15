using System.Text.Json.Serialization;
using TimBearCity.MediaWiki.Common;

namespace TimBearCity.MediaWiki.Pages;

/// <summary>The metadata every page representation carries, whatever content the endpoint returns alongside it.</summary>
/// <param name="Id">The page identifier.</param>
/// <param name="Key">The page title in URL-friendly format.</param>
/// <param name="Title">The page title in reading-friendly format.</param>
/// <param name="Latest">Information about the latest revision.</param>
/// <param name="ContentModel">The type of content on the page, e.g. "wikitext".</param>
/// <param name="License">The license the page content is available under.</param>
public abstract record MediaWikiPageMetadata(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("latest")] MediaWikiRevisionReference Latest,
    [property: JsonPropertyName("content_model")] string ContentModel,
    [property: JsonPropertyName("license")] MediaWikiLicense License);
