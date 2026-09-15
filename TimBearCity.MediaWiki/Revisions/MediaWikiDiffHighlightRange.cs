using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Revisions;

/// <summary>A stretch of a changed line to mark up as added or removed.</summary>
/// <param name="Start">Where the stretch begins, in bytes from the start of the line.</param>
/// <param name="Length">How long the stretch is, in bytes.</param>
/// <param name="Type">Whether the stretch was added or removed.</param>
public sealed record MediaWikiDiffHighlightRange(
    [property: JsonPropertyName("start")] int Start,
    [property: JsonPropertyName("length")] int Length,
    [property: JsonPropertyName("type")] MediaWikiDiffHighlightType Type);
