using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Files;

/// <summary>The file a set of standard thumbnails was derived from.</summary>
/// <remarks>
/// Narrower than <see cref="MediaWikiFileFormat"/>: the thumbnails endpoint reports neither size nor duration.
/// </remarks>
/// <param name="MediaType">The broad kind of media, e.g. "BITMAP".</param>
/// <param name="Url">The URL the file can be downloaded from, which may be protocol-relative.</param>
/// <param name="Width">The width in pixels, if the wiki reports one.</param>
/// <param name="Height">The height in pixels, if the wiki reports one.</param>
public sealed record MediaWikiFileOriginal(
    [property: JsonPropertyName("mediatype")] string MediaType,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("width")] int? Width = null,
    [property: JsonPropertyName("height")] int? Height = null);
