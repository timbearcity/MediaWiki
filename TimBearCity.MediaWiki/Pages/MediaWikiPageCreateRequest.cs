using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Pages;

/// <summary>The request body sent to the page creation endpoint.</summary>
/// <remarks>
/// The optional members are left out of the payload when they are <see langword="null"/>, so the wiki applies its
/// own defaults. The endpoint rejects a body that leaves out <paramref name="Comment"/>, so that one is always
/// sent, even when empty.
/// </remarks>
/// <param name="Title">The title of the page to create.</param>
/// <param name="Source">The page content, in the format named by <paramref name="ContentModel"/>.</param>
/// <param name="Comment">The edit summary, possibly empty.</param>
/// <param name="ContentModel">The content model the source is written in.</param>
/// <param name="Token">The CSRF token, needed only under cookie-based authentication.</param>
internal sealed record MediaWikiPageCreateRequest(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("comment")] string Comment,
    [property: JsonPropertyName("content_model"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ContentModel = null,
    [property: JsonPropertyName("token"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Token = null);
