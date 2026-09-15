using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Revisions;

/// <summary>One side of a revision comparison.</summary>
/// <param name="Id">The revision identifier.</param>
/// <param name="SlotRole">The slot being compared, always "main".</param>
/// <param name="Sections">The revision's sections, so a caller can say which one a change fell in.</param>
public sealed record MediaWikiComparisonRevision(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("slot_role")] string SlotRole,
    [property: JsonPropertyName("sections")] IReadOnlyList<MediaWikiComparisonSection> Sections);
