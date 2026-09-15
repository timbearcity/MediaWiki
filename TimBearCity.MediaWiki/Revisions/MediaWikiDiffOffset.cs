using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Revisions;

/// <summary>Where a line of a comparison sits in each revision, in bytes from the start of the page.</summary>
/// <param name="From">The first byte of the line in the older revision, or <see langword="null"/> if the line is not in it.</param>
/// <param name="To">The first byte of the line in the newer revision, or <see langword="null"/> if the line is not in it.</param>
public sealed record MediaWikiDiffOffset(
    [property: JsonPropertyName("from")] int? From,
    [property: JsonPropertyName("to")] int? To);
