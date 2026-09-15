using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Common;

/// <summary>The license page content is available under.</summary>
/// <remarks>
/// MediaWiki fills both fields from <c>$wgRightsText</c> and <c>$wgRightsUrl</c>, which default to <see langword="null"/>,
/// so a wiki that never configured a license reports both as <see langword="null"/> rather than omitting the object.
/// </remarks>
/// <param name="Title">The name of the license, or <see langword="null"/> if the wiki has not configured one.</param>
/// <param name="Url">A link to the license description, or <see langword="null"/> if the wiki has not configured one.</param>
public sealed record MediaWikiLicense(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("url")] string? Url);
