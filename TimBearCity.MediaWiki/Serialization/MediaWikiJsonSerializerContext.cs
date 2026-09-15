using System.Text.Json;
using System.Text.Json.Serialization;
using TimBearCity.MediaWiki.Files;
using TimBearCity.MediaWiki.Pages;
using TimBearCity.MediaWiki.Revisions;
using TimBearCity.MediaWiki.Search;
using TimBearCity.MediaWiki.Transform;

namespace TimBearCity.MediaWiki.Serialization;

/// <summary>Source-generated metadata for the response models, so the client deserializes without reflection.</summary>
/// <remarks>
/// <see cref="JsonSourceGenerationOptionsAttribute.RespectNullableAnnotations"/> and
/// <see cref="JsonSourceGenerationOptionsAttribute.RespectRequiredConstructorParameters"/> make the serializer honor
/// the nullability of the model records, so a malformed response fails loudly instead of binding
/// <see langword="null"/> into a non-nullable member.
/// </remarks>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(IReadOnlyList<MediaWikiLintError>), TypeInfoPropertyName = "LintErrors")]
[JsonSerializable(typeof(IReadOnlyList<MediaWikiPageLanguageLink>), TypeInfoPropertyName = "PageLanguageLinks")]
[JsonSerializable(typeof(MediaWikiFile))]
[JsonSerializable(typeof(MediaWikiFileThumbnails))]
[JsonSerializable(typeof(MediaWikiPage))]
[JsonSerializable(typeof(MediaWikiPageBare))]
[JsonSerializable(typeof(MediaWikiPageCreateRequest))]
[JsonSerializable(typeof(MediaWikiPageFilesResponse))]
[JsonSerializable(typeof(MediaWikiPageHistory))]
[JsonSerializable(typeof(MediaWikiPageHistoryCount))]
[JsonSerializable(typeof(MediaWikiPageUpdateRequest))]
[JsonSerializable(typeof(MediaWikiPageWithHtml))]
[JsonSerializable(typeof(MediaWikiRevision))]
[JsonSerializable(typeof(MediaWikiRevisionBare))]
[JsonSerializable(typeof(MediaWikiRevisionComparison))]
[JsonSerializable(typeof(MediaWikiRevisionWithHtml))]
[JsonSerializable(typeof(MediaWikiSearchResponse))]
[JsonSerializable(typeof(MediaWikiTransformHtmlRequest))]
[JsonSerializable(typeof(MediaWikiTransformWikitextRequest))]
internal sealed partial class MediaWikiJsonSerializerContext : JsonSerializerContext;
