namespace TimBearCity.MediaWiki.Pages;

/// <summary>The kinds of tally the page history count endpoint can return.</summary>
/// <remarks>
/// The wiki stops counting at a limit that differs by type, and reports having done so through
/// <see cref="MediaWikiPageHistoryCount.IsLimitExceeded"/>. Only <see cref="Edits"/> and <see cref="Editors"/> can be
/// counted between two revisions.
/// </remarks>
public enum MediaWikiPageHistoryCountType
{
    /// <summary>Edits by users who were not logged in.</summary>
    Anonymous,

    /// <summary>Edits by temporary accounts, on wikis that create them for logged-out editors.</summary>
    Temporary,

    /// <summary>Edits by accounts with the bot flag.</summary>
    Bot,

    /// <summary>Distinct users who have edited the page.</summary>
    Editors,

    /// <summary>All edits to the page.</summary>
    Edits,

    /// <summary>Edits their author marked as minor.</summary>
    Minor,

    /// <summary>Edits that were later reverted.</summary>
    Reverted,

    /// <summary>The name the wiki accepted for <see cref="Anonymous"/> before it was renamed; kept as a deprecated alias.</summary>
    AnonymousEdits,

    /// <summary>The name the wiki accepted for <see cref="Bot"/> before it was renamed; kept as a deprecated alias.</summary>
    BotEdits,

    /// <summary>The name the wiki accepted for <see cref="Reverted"/> before it was renamed; kept as a deprecated alias.</summary>
    RevertedEdits
}
