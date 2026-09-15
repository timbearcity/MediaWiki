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

## Commit messages

Messages follow [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/):

```
<type>(<scope>)!: <subject>

<body>

<footers>
```

- `type` is one of the table below. Nothing else is accepted.
- `scope` is optional and names the part touched, for example `client`, `options`, `di`, `tests` or `smoke`.
- `!` marks a breaking change and comes with a `BREAKING CHANGE:` footer saying what breaks and how to migrate. Until 1.0.0 a minor version may break, so
  the marker is what makes those changes findable.
- `subject` is imperative and lowercase (`add`, not `Added` or `Adds`), has no trailing period, and keeps the whole header within 72 characters.
- The body is optional and separated from the header by a blank line. It says what changed and why, not how; the diff shows how. Wrap it at 72 characters.
- Footers are optional: `BREAKING CHANGE: ...`, `Fixes #12`, `Co-Authored-By: ...`.

| Type       | Use                                                                                       | `CHANGELOG.md`      |
|------------|-------------------------------------------------------------------------------------------|---------------------|
| `feat`     | New public API or behaviour                                                               | Added               |
| `fix`      | A bug fix                                                                                 | Fixed               |
| `feat!`, `fix!` | A breaking change                                                                  | Changed or Removed  |
| `docs`     | README, XML docs, `CHANGELOG.md`, this file                                               |                     |
| `test`     | Unit and smoke tests                                                                      |                     |
| `refactor` | A change that neither fixes a bug nor adds behaviour                                      |                     |
| `style`    | Formatting only; prefer squashing it into the change that needed it                       |                     |
| `build`    | Project files, NuGet packages, `Directory.Build.props`, `global.json`                     |                     |
| `ci`       | Workflows and `.github/scripts`                                                           |                     |
| `chore`    | Anything else: the dictionary, `.gitignore`, editor settings                              |                     |

The last column is the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) section a commit of that type lands in, so the changelog for a release
can be compiled from `git log`. Types without a section do not appear in the changelog.

Examples:

```
feat(client): add GetPageHistoryCountAsync
fix(options): reject a UserAgent that is not a well-formed header
feat(options)!: rename Token to AccessToken

BREAKING CHANGE: MediaWikiOptions.Token is now AccessToken; configuration keys change accordingly.
```

Merge commits, reverts and the `fixup!` and `squash!` commits of an interactive rebase are accepted as Git writes them.

The check is `.github/scripts/check-commit-message.sh`. The hook runs it on the message being committed, and the `Commit messages` job in
`.github/workflows/ci.yml` runs it on every commit a push or a pull request brings, so a message that slips past a missing hook still fails the build. Both
use the same script, so they cannot disagree.
