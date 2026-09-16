using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Pages;

/// <summary>The revision a page representation points at as its latest, named just enough to fetch it.</summary>
/// <remarks>
/// For pages in the <c>MediaWiki:</c> namespace the wiki answers with a placeholder rather than a real revision: <see cref="Id"/> is <c>0</c>, as is the
/// page's own id, and <see cref="Timestamp"/> is <see langword="null"/>, even though the page exists and has content.
/// </remarks>
/// <param name="Id">The revision identifier, or <c>0</c> when the wiki does not name a revision.</param>
/// <param name="Timestamp">The time the revision was created, or <see langword="null"/> when the wiki does not name a revision.</param>
public sealed record MediaWikiRevisionReference(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("timestamp")] DateTimeOffset? Timestamp);
