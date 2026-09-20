using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Files;

/// <summary>A file on the wiki, or on the shared repository the wiki draws from.</summary>
/// <remarks>
/// A file used on a page is reported without <paramref name="Thumbnail"/>, which only the file endpoint returns.
/// The renditions are nullable throughout: a wiki that cannot render a derivative, or a media type it has no
/// preferred form for, reports the absence rather than an error.
/// </remarks>
/// <param name="Title">The file's title without the <c>File:</c> namespace, e.g. "Fennec Fox.jpg".</param>
/// <param name="FileDescriptionUrl">The URL of the page describing the file, which may be protocol-relative.</param>
/// <param name="Latest">The most recent upload, if the wiki reports one.</param>
/// <param name="Preferred">The rendition the wiki would rather serve than the original, if it has one.</param>
/// <param name="Original">The file as it was uploaded, if the wiki reports it.</param>
/// <param name="Thumbnail">A small rendition, returned only by the file endpoint and only when the wiki can make one.</param>
public sealed record MediaWikiFile(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("file_description_url")] string FileDescriptionUrl,
    [property: JsonPropertyName("latest")] MediaWikiFileRevision? Latest,
    [property: JsonPropertyName("preferred")] MediaWikiFileFormat? Preferred,
    [property: JsonPropertyName("original")] MediaWikiFileFormat? Original,
    [property: JsonPropertyName("thumbnail")] MediaWikiFileFormat? Thumbnail = null);
