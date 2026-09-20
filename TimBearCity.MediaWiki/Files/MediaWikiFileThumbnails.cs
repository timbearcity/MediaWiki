using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Files;

/// <summary>The standard thumbnails a wiki offers for one file.</summary>
/// <param name="Title">The file's title without the <c>File:</c> namespace, e.g. "Fennec Fox.jpg".</param>
/// <param name="Original">The file the thumbnails were derived from.</param>
/// <param name="Thumbnails">The available thumbnails.</param>
public sealed record MediaWikiFileThumbnails(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("original")] MediaWikiFileOriginal Original,
    [property: JsonPropertyName("thumbnails")] IReadOnlyList<MediaWikiFileThumbnail> Thumbnails);
