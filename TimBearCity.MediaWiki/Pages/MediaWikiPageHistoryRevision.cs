using System.Text.Json.Serialization;
using TimBearCity.MediaWiki.Common;

namespace TimBearCity.MediaWiki.Pages;

/// <summary>One revision as the page history lists it, which is less than a revision endpoint returns.</summary>
/// <param name="Id">The revision identifier.</param>
/// <param name="Timestamp">The time the revision was created.</param>
/// <param name="IsMinor">Whether the edit was marked minor.</param>
/// <param name="Size">The size of the revision in bytes.</param>
/// <param name="Comment">The edit summary, or <see langword="null"/> if there was none or the wiki withheld it.</param>
/// <param name="User">The user who made the edit, or <see langword="null"/> if the wiki withheld it.</param>
/// <param name="Delta">The change in size against the previous revision, or <see langword="null"/> if there is no previous revision to measure against.</param>
public sealed record MediaWikiPageHistoryRevision(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("minor")] bool IsMinor,
    [property: JsonPropertyName("size")] int Size,
    [property: JsonPropertyName("comment")] string? Comment,
    [property: JsonPropertyName("user")] MediaWikiUser? User,
    [property: JsonPropertyName("delta")] int? Delta);
