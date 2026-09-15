using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Pages;

/// <summary>The request body sent to the page update endpoint.</summary>
/// <remarks>
/// The optional members are left out of the payload when they are <see langword="null"/>, so the wiki applies its
/// own defaults. The endpoint rejects a body that leaves out <paramref name="Comment"/>, so that one is always
/// sent, even when empty.
/// </remarks>
/// <param name="Source">The new page content, in the format named by <paramref name="ContentModel"/>.</param>
/// <param name="Comment">The edit summary, possibly empty.</param>
/// <param name="Latest">The revision the edit was based on, which decides whether the endpoint edits or creates.</param>
/// <param name="ContentModel">The content model the source is written in.</param>
/// <param name="Token">The CSRF token, needed only under cookie-based authentication.</param>
internal sealed record MediaWikiPageUpdateRequest(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("comment")] string Comment,
    [property: JsonPropertyName("latest"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] MediaWikiPageUpdateRequest.BaseRevision? Latest = null,
    [property: JsonPropertyName("content_model"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ContentModel = null,
    [property: JsonPropertyName("token"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Token = null)
{
    /// <summary>The revision an edit is based on, as the endpoint wants it named.</summary>
    /// <remarks>
    /// The endpoint accepts the whole <c>latest</c> object a page representation carries and ignores every field
    /// but the identifier, so only that is sent.
    /// </remarks>
    /// <param name="Id">The revision identifier.</param>
    internal sealed record BaseRevision(
        [property: JsonPropertyName("id")] long Id);
}
