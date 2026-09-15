using System.Text.Json.Serialization;

namespace TimBearCity.MediaWiki.Revisions;

/// <summary>One line of a revision comparison.</summary>
/// <param name="Type">What the line represents.</param>
/// <param name="Text">The line itself, empty for a line that only adds or removes a line break.</param>
/// <param name="Offset">Where the line sits in each revision.</param>
/// <param name="LineNumber">The line's number in the newer revision, where the wiki reports one.</param>
/// <param name="HighlightRanges">The stretches to mark up, on a <see cref="MediaWikiDiffType.Changed"/> line.</param>
/// <param name="MoveInfo">The other end of the move, on a <see cref="MediaWikiDiffType.MovedFrom"/> or <see cref="MediaWikiDiffType.MovedTo"/> line.</param>
public sealed record MediaWikiDiffEntry(
    [property: JsonPropertyName("type")] MediaWikiDiffType Type,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("offset")] MediaWikiDiffOffset Offset,
    [property: JsonPropertyName("lineNumber")] int? LineNumber = null,
    [property: JsonPropertyName("highlightRanges")] IReadOnlyList<MediaWikiDiffHighlightRange>? HighlightRanges = null,
    [property: JsonPropertyName("moveInfo")] MediaWikiDiffMoveInfo? MoveInfo = null);
