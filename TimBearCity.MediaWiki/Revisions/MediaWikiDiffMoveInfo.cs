using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Revisions;

/// <summary>The link between the two ends of a paragraph that moved. These occur in pairs within a comparison.</summary>
/// <param name="Id">The identifier of the paragraph this entry describes.</param>
/// <param name="LinkId">The identifier of the paragraph's other end.</param>
/// <param name="LinkDirection">Which way the other end lies, for the arrow a reader follows.</param>
public sealed record MediaWikiDiffMoveInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("linkId")] string LinkId,
    [property: JsonPropertyName("linkDirection")] MediaWikiDiffMoveDirection LinkDirection);
