namespace TimBearCity.MediaWiki.Tests;

/// <summary>The <c>errorKey</c> values MediaWiki's REST API puts in its error bodies, as the tests hand them to the stub and assert on them.</summary>
/// <remarks>
/// The library keeps its own private copies of the keys it acts on; these are independent so that a test still pins the wire value.
/// </remarks>
internal static class MediaWikiErrorKeys
{
    public const string ArticleExists = "apierror-articleexists";
    public const string BadRequest = "rest-bad-request";
    public const string BadTitle = "rest-bad-title";
    public const string CannotLoadFile = "rest-cannot-load-file";
    public const string CompareNonexistent = "rest-compare-nonexistent";
    public const string EditConflict = "editconflict";

    /// <summary>What MediaWiki 1.43 reports an edit conflict as; later versions dropped the hyphen.</summary>
    public const string EditConflictHyphenated = "edit-conflict";

    public const string FileNotThumbnailable = "rest-file-not-thumbnailable";
    public const string InvalidTitle = "rest-invalid-title";
    public const string InvalidTitleOnEdit = "apierror-invalidtitle";
    public const string MissingTitle = "apierror-missingtitle";
    public const string NoMatch = "rest-no-match";
    public const string NoRevision = "rest-no-revision";
    public const string NonexistentRevision = "rest-nonexistent-revision";
    public const string NonexistentTitle = "rest-nonexistent-title";
    public const string NonexistentTitleRevision = "rest-nonexistent-title-revision";
    public const string PageHistoryCountParametersInvalid = "rest-pagehistorycount-parameters-invalid";
    public const string PageHistoryIncompatibleParameters = "rest-pagehistory-incompatible-params";
    public const string PrefixMismatch = "rest-prefix-mismatch";
    public const string UpdateCannotCreatePage = "rest-update-cannot-create-page";
}
