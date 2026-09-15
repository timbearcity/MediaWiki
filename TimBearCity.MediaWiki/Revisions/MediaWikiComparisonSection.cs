using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Revisions;

/// <summary>A section heading in one side of a comparison, as the preprocessor found it.</summary>
/// <param name="Level">The heading level, where 2 is the "==" of a top-level section.</param>
/// <param name="Heading">The heading line in source format, e.g. "==Description==".</param>
/// <param name="Offset">Where the section begins, in bytes from the start of the page.</param>
public sealed record MediaWikiComparisonSection(
    [property: JsonPropertyName("level")] int Level,
    [property: JsonPropertyName("heading")] string Heading,
    [property: JsonPropertyName("offset")] int Offset);
