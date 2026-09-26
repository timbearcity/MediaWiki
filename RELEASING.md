# Releasing

A release is a signed `v*` tag pushed to `main`. The Release workflow builds and tests the tagged commit, publishes the package to NuGet and creates the
GitHub release; the Docs workflow adds the version to the site. Everything before the tag, and the baseline bump after it, is done by hand as below.

The commands use `X.Y.Z` for the version being released and `A.B.C` for the one before it. Until 1.0.0, a release with a breaking change or new API
bumps the minor version and one with only fixes bumps the patch version. A version with a suffix, such as `1.2.3-beta.1`, is published as a pre-release.

## 1. Check `main`

```
git switch main
git pull --ff-only
git log --oneline vA.B.C..HEAD
gh run list --branch main --limit 3
```

- The working tree is clean and CI passed on the last commit.
- Every `feat`, `fix` and breaking commit in the log has an entry under `## [Unreleased]` in `CHANGELOG.md`, in the section the table in
  [CONTRIBUTING.md](CONTRIBUTING.md#commit-messages) gives its type, and each breaking change says how to upgrade.

Pushes do not run the smoke tests against the English Wikipedia; run them once before tagging and wait for the result:

```
gh workflow run ci.yml --ref main -f smoke=true
gh run watch
```

## 2. Prepare the release commit

Move the public API added since the last release into `PublicAPI.Shipped.txt`:

```
pwsh .github/scripts/mark-shipped.ps1
```

In `CHANGELOG.md`, add the version heading under `## [Unreleased]`, leaving that section empty, and update the link references at the end:

```
## [Unreleased]

## [X.Y.Z] - YYYY-MM-DD
```

```
[Unreleased]: https://github.com/timbearcity/MediaWiki/compare/vX.Y.Z...HEAD
[X.Y.Z]: https://github.com/timbearcity/MediaWiki/releases/tag/vX.Y.Z
[A.B.C]: https://github.com/timbearcity/MediaWiki/releases/tag/vA.B.C
```

Set `<Version>X.Y.Z</Version>` in `Directory.Build.props`. Then check that the release notes come out, that the tests pass and that package validation
accepts the package against the baseline:

```
python3 .github/scripts/changelog-section.py X.Y.Z
dotnet test
dotnet pack --configuration Release --output artifacts -p:Version=X.Y.Z
```

The Release workflow runs the first and last of these too, so a failure here would stop the release there. Commit the changes on `main` and push them:

```
git add CHANGELOG.md Directory.Build.props TimBearCity.MediaWiki/PublicAPI.Shipped.txt TimBearCity.MediaWiki/PublicAPI.Unshipped.txt
git commit -m "chore(release): prepare X.Y.Z"
git push origin main
```

## 3. Tag the release

Wait for CI to pass on the prepare commit, then tag it:

```
gh run watch
git tag -s vX.Y.Z -m vX.Y.Z
git push origin vX.Y.Z
```

## 4. Watch the workflows

```
gh run list --limit 4
gh run watch
```

The Release workflow runs three jobs in turn:

1. **Verify** runs CI on the tagged commit.
2. **Publish to NuGet** waits for approval, since the `nuget` environment has a required reviewer; approve it from the run page. It then packs with the
   tag's version, extracts the release notes and pushes the package.
3. **Create GitHub release** creates the release `vX.Y.Z` with the notes and the packages attached.

The Docs workflow publishes the site with the new version in the picker. Once both have finished, check the result:

```
curl -s https://api.nuget.org/v3-flatcontainer/timbearcity.mediawiki/index.json
gh release view vX.Y.Z
```

NuGet can take several minutes to index a new version, and longer before the package page shows it.

### If a job fails

- **Verify, or the release notes step of Publish:** nothing was published. Fix it on `main`, delete the tag and go through step 3 again:

  ```
  git tag -d vX.Y.Z
  git push origin :refs/tags/vX.Y.Z
  ```

- **Push to NuGet, or Create GitHub release:** rerun the failed jobs with `gh run rerun <run-id> --failed`. The push skips a package that is already on
  NuGet, so a rerun after a partial upload pushes only what is missing.
- **Docs:** rerun it, or run it by hand with `gh workflow run docs.yml`; it builds every release from its tag.

A NuGet version can be unlisted but never deleted or replaced. If a broken package got out, release a patch version rather than reusing the number.

## 5. Move the package validation baseline

Once `X.Y.Z` is on NuGet, make it the baseline so that every pull request from now on is checked against it. Set
`<PackageValidationBaselineVersion>X.Y.Z</PackageValidationBaselineVersion>` in `TimBearCity.MediaWiki/TimBearCity.MediaWiki.csproj`. If
`TimBearCity.MediaWiki/CompatibilitySuppressions.xml` exists, delete it: it suppresses breaks between the old baseline and `X.Y.Z`, which the new baseline
already ships, and package validation fails on a suppression that matches nothing. Check and commit:

```
dotnet pack --configuration Release --output artifacts
git add TimBearCity.MediaWiki
git commit -m "build(package): validate against X.Y.Z"
git push origin main
```
