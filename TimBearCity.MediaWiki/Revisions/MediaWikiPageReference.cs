using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Revisions;

/// <summary>The page a revision belongs to, named just enough to fetch it.</summary>
/// <param name="Id">The page identifier.</param>
/// <param name="Key">The page title in URL-friendly format.</param>
/// <param name="Title">The page title in reading-friendly format.</param>
public sealed record MediaWikiPageReference(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title);
