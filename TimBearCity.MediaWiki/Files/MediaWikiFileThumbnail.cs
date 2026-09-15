using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Files;

/// <summary>One of the standard thumbnail sizes a wiki renders a file at.</summary>
/// <param name="MimeType">The media type of the thumbnail, e.g. "image/jpeg".</param>
/// <param name="Url">The URL the thumbnail can be downloaded from, which may be protocol-relative.</param>
/// <param name="Width">The thumbnail's width in pixels.</param>
/// <param name="Height">The thumbnail's height in pixels.</param>
/// <param name="ResponsiveUrls">Higher-density renderings of the same thumbnail, keyed by pixel ratio, e.g. "2".</param>
public sealed record MediaWikiFileThumbnail(
    [property: JsonPropertyName("mime")] string MimeType,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("responsive_urls")] IReadOnlyDictionary<string, string>? ResponsiveUrls = null);
