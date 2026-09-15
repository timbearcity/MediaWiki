using System.Text.Json.Serialization;
using TimBearCity.MediaWiki.Common;

namespace TimBearCity.MediaWiki.Revisions;

/// <summary>A revision, including its source.</summary>
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
/// <param name="Source">The revision content in the format specified by <paramref name="ContentModel"/>.</param>
public sealed record MediaWikiRevision(
    long Id,
    int Size,
    bool IsMinor,
    DateTimeOffset Timestamp,
    string ContentModel,
    MediaWikiPageReference Page,
    MediaWikiLicense License,
    MediaWikiUser? User,
    string? Comment,
    int? Delta,
    [property: JsonPropertyName("source")] string Source)
    : MediaWikiRevisionMetadata(Id, Size, IsMinor, Timestamp, ContentModel, Page, License, User, Comment, Delta);
