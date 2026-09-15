namespace TimBearCity.MediaWiki.Revisions;

/// <summary>Which way a reader travels to reach the other half of a moved paragraph.</summary>
public enum MediaWikiDiffMoveDirection
{
    /// <summary>The paragraph this one links to sits further down the page.</summary>
    Lower = 0,

    /// <summary>The paragraph this one links to sits further up the page.</summary>
    Higher = 1
}
