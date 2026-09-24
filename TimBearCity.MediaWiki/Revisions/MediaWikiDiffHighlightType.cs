namespace TimBearCity.MediaWiki.Revisions;

/// <summary>What a highlighted stretch of a changed line represents.</summary>
/// <remarks>
/// The value is bound from the number the wiki sends, without checking it against the members here, so a wiki whose wikidiff2 adds a highlight type
/// passes it through as a value outside them. Give a <see langword="switch"/> over it a default arm.
/// </remarks>
public enum MediaWikiDiffHighlightType
{
    /// <summary>Text the newer revision adds.</summary>
    Addition = 0,

    /// <summary>Text the newer revision drops.</summary>
    Deletion = 1
}
