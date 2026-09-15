using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Serialization;

/// <summary>Source-generated metadata for <see cref="MediaWikiError"/>, kept separate so it can stay lenient.</summary>
/// <remarks>
/// Error bodies are best-effort diagnostics, so they are read without the nullability checks the response models get:
/// a wiki that omits a field, or answers in a shape this library does not know, should still yield a useful message
/// rather than a second failure.
/// </remarks>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(MediaWikiError))]
internal sealed partial class MediaWikiErrorJsonSerializerContext : JsonSerializerContext;
