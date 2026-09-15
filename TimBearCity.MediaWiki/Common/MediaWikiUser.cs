using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Common;

/// <summary>The account behind a revision or an upload.</summary>
/// <remarks>
/// Both members are nullable because the wiki reports what it can: an anonymous edit carries an address in place
/// of a name and no identifier, and a suppressed one carries neither.
/// </remarks>
/// <param name="Id">The user identifier, or <see langword="null"/> for an anonymous or hidden user.</param>
/// <param name="Name">The username, the originating IP address for an anonymous user, or <see langword="null"/> if the wiki withheld it.</param>
public sealed record MediaWikiUser(
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("name")] string? Name);
