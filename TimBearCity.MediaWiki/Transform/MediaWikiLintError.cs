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
/// typed member, since the linter defines it per error type. Check its <see cref="JsonElement.ValueKind"/> before
/// reading it: a kind with detail sends an object, such as <c>{"name":"div"}</c>; a kind with none sends an empty
/// array, on which <see cref="JsonElement.TryGetProperty(string, out JsonElement)"/> throws just as
/// <see cref="JsonElement.GetProperty(string)"/> does; and it is <see cref="JsonValueKind.Undefined"/> if the wiki
/// leaves the field out.
/// </param>
public sealed record MediaWikiLintError(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("dsr")] IReadOnlyList<int?> Dsr,
    [property: JsonPropertyName("templateInfo")] MediaWikiLintTemplateInfo? TemplateInfo = null,
    [property: JsonPropertyName("params")] JsonElement Params = default);
