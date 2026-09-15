using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Pages;

/// <summary>How many revisions of one kind a page has.</summary>
/// <param name="Count">The number counted.</param>
/// <param name="IsLimitExceeded">Whether the real number is higher than <paramref name="Count"/>, because counting stopped at the wiki's limit.</param>
public sealed record MediaWikiPageHistoryCount(
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("limit")] bool IsLimitExceeded);
