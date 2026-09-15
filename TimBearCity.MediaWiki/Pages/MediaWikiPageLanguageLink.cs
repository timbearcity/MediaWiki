using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Pages;

/// <summary>The same page on another wiki in the same family, as its interlanguage links name it.</summary>
/// <param name="Code">The language code, e.g. "ms".</param>
/// <param name="Name">The language's name in its own language, e.g. "Bahasa Melayu".</param>
/// <param name="Key">The translated page title in URL-friendly format.</param>
/// <param name="Title">The translated page title in reading-friendly format.</param>
public sealed record MediaWikiPageLanguageLink(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title);
