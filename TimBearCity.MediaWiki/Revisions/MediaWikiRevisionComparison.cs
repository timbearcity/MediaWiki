using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Revisions;

/// <summary>The difference between two revisions of a page.</summary>
/// <param name="From">The revision used as the base.</param>
/// <param name="To">The revision compared against it.</param>
/// <param name="Diff">The differing lines, with up to two lines of context around each change.</param>
public sealed record MediaWikiRevisionComparison(
    [property: JsonPropertyName("from")] MediaWikiComparisonRevision From,
    [property: JsonPropertyName("to")] MediaWikiComparisonRevision To,
    [property: JsonPropertyName("diff")] IReadOnlyList<MediaWikiDiffEntry> Diff);
