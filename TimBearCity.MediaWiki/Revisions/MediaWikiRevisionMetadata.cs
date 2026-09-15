using System.Text.Json.Serialization;
using TimBearCity.MediaWiki.Common;

namespace TimBearCity.MediaWiki.Revisions;

/// <summary>The metadata every revision representation carries, whatever content the endpoint returns alongside it.</summary>
/// <param name="Id">The revision identifier.</param>
/// <param name="Size">The size of the revision in bytes.</param>
/// <param name="IsMinor">Whether the edit was marked minor.</param>
/// <param name="Timestamp">The time the revision was created.</param>
/// <param name="ContentModel">The type of content in the revision, e.g. "wikitext".</param>
/// <param name="Page">The page the revision belongs to.</param>
/// <param name="License">The license the content is available under.</param>
/// <param name="User">The user who made the edit, or <see langword="null"/> if the wiki withheld it.</param>
/// <param name="Comment">The edit summary, or <see langword="null"/> if there was none or the wiki withheld it.</param>
/// <param name="Delta">The change in size against the previous revision, or <see langword="null"/> if there is no previous revision to measure against.</param>
public abstract record MediaWikiRevisionMetadata(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("size")] int Size,
    [property: JsonPropertyName("minor")] bool IsMinor,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("content_model")] string ContentModel,
    [property: JsonPropertyName("page")] MediaWikiPageReference Page,
    [property: JsonPropertyName("license")] MediaWikiLicense License,
    [property: JsonPropertyName("user")] MediaWikiUser? User,
    [property: JsonPropertyName("comment")] string? Comment,
    [property: JsonPropertyName("delta")] int? Delta);
