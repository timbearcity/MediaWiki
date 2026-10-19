# Contributing

## Setup

Two Git settings are per clone, so they are not versioned; run both once after cloning:

```
git config core.hooksPath .githooks
git config commit.template .gitmessage
```

The first installs a `commit-msg` hook that checks each message against the convention below before the commit is made. The second pre-fills the editor
with a reminder of the format. Rider runs Git hooks, so the check applies to commits made from the IDE too.

In Rider, under Settings > Version Control > Commit, enable the "Blank line between subject and body", "Limit subject line" (72) and "Limit body line"
(72) inspections; they flag in the editor what the hook rejects afterwards. Rider reads `commit.template` too, so the reminder appears in the commit field.

## Documentation

The site at [timbearcity.github.io/MediaWiki](https://timbearcity.github.io/MediaWiki/) is the README, `CHANGELOG.md` and this file as pages, plus an API
reference generated from the XML documentation comments. `docs/docfx.json` configures [docfx](https://dotnet.github.io/docfx/), which is pinned in
`.config/dotnet-tools.json`. To preview a change locally:

```
dotnet tool restore
dotnet docfx docs/docfx.json --serve
```

The site holds one copy per release, `v0.2.0/` and so on, with a version picker in the navbar; the root redirects to the newest. The Docs workflow
publishes it to GitHub Pages when a `v*` tag is pushed, building every release's source with the docs config and template of the commit it runs on, so a
fix to `docs/` on `main` reaches the site at the next tag or when the workflow is run by hand. On every pull request, whatever it changes, it builds the
current docs with warnings as errors, plus every release, without publishing.

## Commit messages

Messages follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/):

```
<type>(<scope>)!: <subject>

<body>

<footers>
```

- `type` is one of the type table below. Nothing else is accepted.
- `scope` is optional and names the part touched. It is one of the scope table below; nothing else is accepted.
- `!` marks a breaking change and comes with a `BREAKING CHANGE:` footer, spelled with a space rather than a hyphen, saying what breaks and how to
  migrate; neither is accepted without the other. Until 1.0.0 a minor version may break, so the marker is what makes those changes findable.
- `subject` is imperative and lowercase (`add`, not `Added` or `Adds`), has no trailing period, and keeps the whole header within 72 characters. The
  ` (#N)` that GitHub appends when a pull request is squash-merged does not count.
- The body is optional and separated from the header by a blank line. It says what changed and why, not how; the diff shows how. Wrap it at 72
  characters; only a line holding a URL or a trailer such as `Co-authored-by:` may run longer.
- Footers are optional: `BREAKING CHANGE: ...`, `Fixes #12`, `Co-Authored-By: ...`. An issue is closed by a line of its own reading `Fixes #12`, one per
  issue, after a blank line or another such line. Any other closing keyword before an issue number, such as `Closes #12`, `Fixes: #12` or a `fixes #12`
  inside a sentence, is refused, since GitHub would still close the issue.

| Type            | Use                                                                   | `CHANGELOG.md`     |
|-----------------|-----------------------------------------------------------------------|--------------------|
| `feat`          | New public API or behaviour                                           | Added              |
| `fix`           | A bug fix                                                             | Fixed              |
| `feat!`, `fix!` | A breaking change                                                     | Changed or Removed |
| `docs`          | README, XML docs, `CHANGELOG.md`, this file, the site in `docs/`      |                    |
| `test`          | Unit and smoke tests                                                  |                    |
| `refactor`      | A change that neither fixes a bug nor adds behaviour                  |                    |
| `style`         | Formatting only; prefer squashing it into the change that needed it   |                    |
| `build`         | Project files, NuGet packages, `Directory.Build.props`, `global.json` |                    |
| `ci`            | Workflows and `.github/scripts`                                       |                    |
| `chore`         | Anything else: the dictionary, `.gitignore`, editor settings          |                    |

The last column is the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) section a commit of that type lands in, so the changelog for a release
can be compiled from `git log`. Types without a section do not appear in the changelog.

| Area                  | Scopes                                                                              |
|-----------------------|-------------------------------------------------------------------------------------|
| The library           | `auth`, `client`, `diagnostics`, `errors`, `files`, `options`, `pages`, `revisions` |
| Tests                 | `smoke`, `tests`                                                                    |
| Build and release     | `deps`, `package`, `release`, `sln`                                                 |
| CI and the repository | `commit-check`, `dependabot`, `github`, `scripts`                                   |
| Documentation         | `contributing`, `docs`, `readme`, `site`                                            |
| Editor settings       | `editorconfig`, `rider`                                                             |

A new area adds its scope to this table and to the check below, which keeps its own copy of the types and scopes.

Examples:

```
feat(client): add GetPageHistoryCountAsync
fix(options): reject a UserAgent that is not a well-formed header
feat(options)!: rename Token to AccessToken

BREAKING CHANGE: MediaWikiOptions.Token is now AccessToken; configuration keys change accordingly.
```

Merge commits, reverts and the `fixup!` and `squash!` commits of an interactive rebase are accepted as Git writes them, and so is the `* <header>` line
GitHub writes for each commit in the body of a squash merge.

The check is `.github/scripts/check-commit-message.sh`. The hook runs it on the message being committed, and the `Commit messages` job in
`.github/workflows/ci.yml` runs it on every commit a push or a pull request brings, so a message that slips past a missing hook still fails the build. Both
use the same script, so they cannot disagree.

The job skips Dependabot's pull requests, since its NuGet updates start the subject with `Bump` whatever the prefix. Lowercase the title when
squash-merging one; the push to `main` checks it like any other commit. That casing is a Dependabot bug, and the skip goes once its fix,
[dependabot/dependabot-core#16310](https://github.com/dependabot/dependabot-core/pull/16310), is released.

## Releasing

The steps for cutting a release, from the changelog to moving the package validation baseline afterwards, are in
[RELEASING.md](https://github.com/timbearcity/MediaWiki/blob/main/RELEASING.md).
