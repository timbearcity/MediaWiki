namespace TimBearCity.MediaWiki.Revisions;

/// <summary>Which way a reader travels to reach the other half of a moved paragraph.</summary>
/// <remarks>
/// The value is bound from the number the wiki sends, without checking it against the members here, so a wiki whose wikidiff2 adds a direction passes
/// it through as a value outside them. Give a <see langword="switch"/> over it a default arm.
/// </remarks>
public enum MediaWikiDiffMoveDirection
{
    /// <summary>The paragraph this one links to sits further down the page.</summary>
    Lower = 0,

    /// <summary>The paragraph this one links to sits further up the page.</summary>
    Higher = 1
}
