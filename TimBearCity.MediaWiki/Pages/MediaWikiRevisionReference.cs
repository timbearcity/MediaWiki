using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Pages;

/// <summary>The revision a page representation points at as its latest, named just enough to fetch it.</summary>
/// <param name="Id">The revision identifier.</param>
/// <param name="Timestamp">The time the revision was created.</param>
public sealed record MediaWikiRevisionReference(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp);
