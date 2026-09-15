using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Transform;

/// <summary>The template a lint error was found inside, when it was not found in the page's own source.</summary>
/// <remarks>The wiki reports one of the two members, never both.</remarks>
/// <param name="Name">The template's name.</param>
/// <param name="IsMultiPartTemplateBlock">True when the error spans more than one template, leaving none to name.</param>
public sealed record MediaWikiLintTemplateInfo(
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("multiPartTemplateBlock")] bool? IsMultiPartTemplateBlock = null);
