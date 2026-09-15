using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Transform;

/// <summary>The request body sent to the transform endpoints that take wikitext.</summary>
/// <param name="Wikitext">The wikitext to convert.</param>
internal sealed record MediaWikiTransformWikitextRequest(
    [property: JsonPropertyName("wikitext")] string Wikitext);
