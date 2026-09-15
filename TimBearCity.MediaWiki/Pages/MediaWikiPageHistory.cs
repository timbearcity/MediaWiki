using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Pages;

/// <summary>One page of a page's revision history, newest first.</summary>
/// <remarks>
/// The endpoint returns at most 20 revisions at a time. <paramref name="Older"/> and <paramref name="Newer"/> are
/// the wiki's own links to the adjacent pages; to walk the history with this client, take the identifier of the
/// first or last revision you were given and pass it back as the corresponding argument.
/// </remarks>
/// <param name="Revisions">The revisions in this page of the history, newest first.</param>
/// <param name="Latest">The absolute URL of the first page of the history.</param>
/// <param name="Older">The absolute URL of the next page of older revisions, or <see langword="null"/> if this is the end of the history.</param>
/// <param name="Newer">The absolute URL of the next page of newer revisions, or <see langword="null"/> if this is the start of the history.</param>
public sealed record MediaWikiPageHistory(
    [property: JsonPropertyName("revisions")] IReadOnlyList<MediaWikiPageHistoryRevision> Revisions,
    [property: JsonPropertyName("latest")] string Latest,
    [property: JsonPropertyName("older")] string? Older = null,
    [property: JsonPropertyName("newer")] string? Newer = null);
