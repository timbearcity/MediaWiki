namespace TimBearCity.MediaWiki.Pages;

/// <summary>The kinds of revision a page history can be narrowed to.</summary>
public enum MediaWikiPageHistoryFilter
{
    /// <summary>Revisions made by users who were not logged in.</summary>
    Anonymous,

    /// <summary>Revisions made by accounts with the bot flag.</summary>
    Bot,

    /// <summary>Revisions that were later reverted.</summary>
    Reverted,

    /// <summary>Revisions their author marked as minor.</summary>
    Minor
}
