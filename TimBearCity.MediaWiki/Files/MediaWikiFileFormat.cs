using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Files;

/// <summary>One rendition of a file: the upload itself, or a derivative the wiki offers in its place.</summary>
/// <param name="MediaType">The broad kind of media, e.g. "BITMAP", "VIDEO" or "AUDIO".</param>
/// <param name="Url">The URL the rendition can be downloaded from, which may be protocol-relative.</param>
/// <param name="Size">The size in bytes, if the wiki reports one.</param>
/// <param name="Width">The width in pixels, if the wiki reports one.</param>
/// <param name="Height">The height in pixels, if the wiki reports one.</param>
/// <param name="Duration">The length in seconds for audio, video and multimedia; null for other media.</param>
public sealed record MediaWikiFileFormat(
    [property: JsonPropertyName("mediatype")] string MediaType,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("size")] long? Size = null,
    [property: JsonPropertyName("width")] int? Width = null,
    [property: JsonPropertyName("height")] int? Height = null,
    [property: JsonPropertyName("duration")] double? Duration = null);
