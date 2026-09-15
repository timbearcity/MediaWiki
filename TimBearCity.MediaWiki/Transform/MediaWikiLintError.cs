using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Transform;

/// <summary>A problem the wiki's linter found in a page's markup.</summary>
/// <param name="Type">The kind of problem, e.g. "obsolete-tag".</param>
/// <param name="Dsr">
/// Where the problem is, in bytes: the start offset, the end offset, the width of the opening tag and the width of
/// the closing tag, optionally followed by the widths of the whitespace inside each. Entries the linter could not
/// determine are null.
/// </param>
/// <param name="TemplateInfo">The template the problem came from, or <see langword="null"/> if it is in the page's own source.</param>
/// <param name="Params">
/// Detail particular to <paramref name="Type"/>, whose shape varies by kind. It arrives as raw JSON rather than a
/// typed member, since the linter defines it per error type; read it with <see cref="JsonElement"/>'s own members.
/// </param>
public sealed record MediaWikiLintError(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("dsr")] IReadOnlyList<int?> Dsr,
    [property: JsonPropertyName("templateInfo")] MediaWikiLintTemplateInfo? TemplateInfo = null,
    [property: JsonPropertyName("params")] JsonElement Params = default);
