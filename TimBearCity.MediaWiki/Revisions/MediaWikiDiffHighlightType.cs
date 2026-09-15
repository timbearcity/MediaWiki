namespace TimBearCity.MediaWiki.Revisions;

/// <summary>What a highlighted stretch of a changed line represents.</summary>
public enum MediaWikiDiffHighlightType
{
    /// <summary>Text the newer revision adds.</summary>
    Addition = 0,

    /// <summary>Text the newer revision drops.</summary>
    Deletion = 1
}
