using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Search;

/// <summary>A preview image, or other media, associated with a search result.</summary>
/// <param name="MimeType">The media type of the file, e.g. "image/jpeg".</param>
/// <param name="Url">A protocol-relative URL the file can be downloaded from.</param>
/// <param name="Size">The file size in bytes, if the wiki reports one.</param>
/// <param name="Width">The recommended width in pixels, if the wiki reports one.</param>
/// <param name="Height">The recommended height in pixels, if the wiki reports one.</param>
/// <param name="Duration">The length in seconds for audio and video files; null for other media.</param>
public sealed record MediaWikiSearchResultThumbnail(
    [property: JsonPropertyName("mimetype")] string MimeType,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("size")] long? Size = null,
    [property: JsonPropertyName("width")] int? Width = null,
    [property: JsonPropertyName("height")] int? Height = null,
    [property: JsonPropertyName("duration")] int? Duration = null);
