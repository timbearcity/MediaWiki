using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Search;

/// <summary>The transport envelope returned by the search endpoint.</summary>
/// <param name="Pages">The matching pages, in relevance order.</param>
internal sealed record MediaWikiSearchResponse(
    [property: JsonPropertyName("pages")] IReadOnlyList<MediaWikiSearchResult> Pages);
