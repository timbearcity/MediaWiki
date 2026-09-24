namespace TimBearCity.MediaWiki.Revisions;

/// <summary>What a line in a revision comparison represents.</summary>
/// <remarks>
/// The value is bound from the number the wiki sends, without checking it against the members here, so a wiki whose wikidiff2 adds a line type passes
/// it through as a value outside them. Give a <see langword="switch"/> over it a default arm.
/// </remarks>
public enum MediaWikiDiffType
{
    /// <summary>Unchanged, included as context around a change; up to two such lines surround each one.</summary>
    Context = 0,

    /// <summary>Present in the newer revision only.</summary>
    Added = 1,

    /// <summary>Present in the older revision only.</summary>
    Removed = 2,

    /// <summary>Present in both, with text that differs; see <see cref="MediaWikiDiffEntry.HighlightRanges"/>.</summary>
    Changed = 3,

    /// <summary>Where moved content sat in the older revision.</summary>
    MovedFrom = 4,

    /// <summary>Where moved content sits in the newer revision.</summary>
    MovedTo = 5
}
