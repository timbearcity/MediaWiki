using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Search;

/// <summary>One page matched by a search.</summary>
/// <param name="Id">The page identifier.</param>
/// <param name="Key">The page title in URL-friendly format.</param>
/// <param name="Title">The page title in reading-friendly format.</param>
/// <param name="Excerpt">
/// An HTML snippet of the matching text, with the search terms wrapped in &lt;span class="searchmatch"&gt;; the title instead, when the
/// match came from a title search.
/// </param>
/// <param name="MatchedTitle">The title of the redirect that matched, if the match came via a redirect.</param>
/// <param name="Anchor">The section of the page the matching redirect points at, if the match came via such a redirect.</param>
/// <param name="Description">A short description of the page, if one exists.</param>
/// <param name="Thumbnail">A preview image for the page, if it has one.</param>
public sealed record MediaWikiSearchResult(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("excerpt")] string? Excerpt = null,
    [property: JsonPropertyName("matched_title")] string? MatchedTitle = null,
    [property: JsonPropertyName("anchor")] string? Anchor = null,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("thumbnail")] MediaWikiSearchResultThumbnail? Thumbnail = null);
