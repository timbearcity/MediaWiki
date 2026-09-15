using System.Text.Json.Serialization;
using TimBearCity.MediaWiki.Files;

namespace TimBearCity.MediaWiki.Pages;

/// <summary>The transport envelope returned by the endpoint listing the files used on a page.</summary>
/// <param name="Files">The files, in the order the wiki reports them.</param>
internal sealed record MediaWikiPageFilesResponse(
    [property: JsonPropertyName("files")] IReadOnlyList<MediaWikiFile> Files);
