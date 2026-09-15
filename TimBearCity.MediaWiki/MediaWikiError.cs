using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki;

/// <summary>The error body returned by the MediaWiki REST API on a failed request.</summary>
/// <param name="ErrorKey">A machine-readable identifier for the error, e.g. <c>rest-nonexistent-title</c>.</param>
/// <param name="MessageTranslations">The human-readable message, keyed by language code.</param>
/// <param name="Message">The human-readable message, on endpoints that return an untranslated one.</param>
/// <param name="HttpReason">The reason phrase MediaWiki associated with the status code.</param>
internal sealed record MediaWikiError(
    [property: JsonPropertyName("errorKey")] string? ErrorKey = null,
    [property: JsonPropertyName("messageTranslations")] IReadOnlyDictionary<string, string>? MessageTranslations = null,
    [property: JsonPropertyName("message")] string? Message = null,
    [property: JsonPropertyName("httpReason")] string? HttpReason = null)
{
    /// <summary>The best available human-readable description, or <see langword="null"/> if the body carried none.</summary>
    public string? Describe()
    {
        if (MessageTranslations is not { Count: > 0 })
        {
            return string.IsNullOrWhiteSpace(Message) ? null : Message;
        }

        if (MessageTranslations.TryGetValue("en", out var english) && !string.IsNullOrWhiteSpace(english))
        {
            return english;
        }

        foreach (var translation in MessageTranslations.Values)
        {
            if (!string.IsNullOrWhiteSpace(translation))
            {
                return translation;
            }
        }

        return string.IsNullOrWhiteSpace(Message) ? null : Message;
    }
}
