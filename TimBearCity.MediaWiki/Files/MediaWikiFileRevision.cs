using System.Text.Json.Serialization;
using TimBearCity.MediaWiki.Common;

namespace TimBearCity.MediaWiki.Files;

/// <summary>The most recent upload of a file.</summary>
/// <param name="Timestamp">The time the file was last uploaded.</param>
/// <param name="User">The user who uploaded it, or <see langword="null"/> if the wiki withheld it.</param>
public sealed record MediaWikiFileRevision(
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("user")] MediaWikiUser? User);
