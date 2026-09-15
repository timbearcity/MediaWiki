using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Transform;

/// <summary>The request body sent to the transform endpoint that takes HTML.</summary>
/// <param name="Html">The HTML to convert.</param>
internal sealed record MediaWikiTransformHtmlRequest(
    [property: JsonPropertyName("html")] string Html);
